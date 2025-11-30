using Microsoft.AspNetCore.Cryptography.KeyDerivation;
using Microsoft.Extensions.Configuration;
using System.Security.Cryptography;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using DeBillPay_Backend.Data;
using DeBillPay_Backend.Models;
using Microsoft.EntityFrameworkCore;
using DeBillPay_Backend.DTOs;
using DeBillPay_Backend.Services;
using DeBillPay_Backend.Models.Validation;
using Microsoft.AspNetCore.Mvc;

namespace DeBillPay_Backend.Endpoints
{
	public static class EbillHistoryEndpoints
	{
		public static void MapEbillHistoryEndpoints(this WebApplication app)
		{

			app.MapGet("/api/ebills/{ebillId:int}/history", async (int ebillId, HttpContext http, ApplicationDbContext db) =>
			{
				var userIdClaim = http.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
				if (userIdClaim == null)
					return Results.Unauthorized();

				int userId = int.Parse(userIdClaim);

				var currentUser = await db.Users.FindAsync(userId);
				if (currentUser == null)
					return Results.NotFound("User not found");

				var fullName = $"{currentUser.FirstName} {currentUser.LastName}";

				var histories = await db.EbillHistories
					.Where(h => h.EbillId == ebillId)
					.OrderByDescending(h => h.CreatedAt)
					.Select(h => new
					{
						h.EbillHistoryId,
						h.EbillId,
						h.UserId,
						Action = h.ActionType,
						Message = h.Message.Contains(fullName)
							? (h.ActionType == "full_payment" || h.ActionType == "partial_payment"
								? h.Message.Replace(fullName, "ви")
								: h.Message.Replace(fullName, "вас"))
							: h.Message,
						h.CreatedAt
					})
					.ToListAsync();

				return Results.Ok(histories);

			})
.RequireAuthorization();


		}
	}
}

