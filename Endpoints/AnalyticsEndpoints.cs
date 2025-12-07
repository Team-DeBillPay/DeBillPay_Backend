using System.Security.Claims;
using DeBillPay_Backend.Data;
using Microsoft.EntityFrameworkCore;

namespace DeBillPay_Backend.Endpoints
{
	public static class AnalyticsEndpoints
	{
		public static void MapAnalyticsEndpoints(this WebApplication app)
		{
			app.MapGet("/api/analytics/debts-flow/monthly/6", async (
				HttpContext http,
				ApplicationDbContext db
			) =>
			{
				return await GetDebtsFlow(http, db, 6);
			}).RequireAuthorization();

			app.MapGet("/api/analytics/debts-flow/monthly/12", async (
				HttpContext http,
				ApplicationDbContext db
			) =>
			{
				return await GetDebtsFlow(http, db, 12);
			}).RequireAuthorization();
		}

		private static async Task<IResult> GetDebtsFlow(
			HttpContext http,
			ApplicationDbContext db,
			int months)
		{
			var userId = http.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
			if (userId is null)
				return Results.Unauthorized();

			int myId = int.Parse(userId);

			DateTime now = DateTime.UtcNow;
			DateTime from = now.AddMonths(-months);

			var ebills = await db.Ebills
				.Where(e =>
					(e.OrganizerId == myId ||
					 e.Participants.Any(p => p.UserId == myId)) &&
					e.CreatedAt >= from
				)
				.Select(e => new
				{
					Month = new DateTime(e.CreatedAt.Year, e.CreatedAt.Month, 1),

					Participants = e.Participants.Select(p => new
					{
						p.UserId,
						p.AssignedAmount,
						p.PaidAmount
					}).ToList()
				})
				.ToListAsync();

			var grouped = ebills
				.GroupBy(e => e.Month)
				.Select(g =>
				{
					decimal whatILent = 0;
					decimal whatIOwe = 0;

					foreach (var ebill in g)
					{
						foreach (var p in ebill.Participants)
						{
							if (p.UserId == myId)
							{
								whatIOwe += p.AssignedAmount - p.PaidAmount;
							}
							else
							{
								whatILent += p.AssignedAmount - p.PaidAmount;
							}
						}
					}

					if (whatILent < 0) whatILent = 0;
					if (whatIOwe < 0) whatIOwe = 0;

					return new
					{
						month = g.Key.ToString("yyyy-MM"),
						whatILent,
						whatIOwe
					};
				})
				.OrderBy(r => r.month);

			return Results.Ok(grouped);
		}
	}
}