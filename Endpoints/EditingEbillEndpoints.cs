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

public static class EditingEbillEndpoints
{
	public static void MapEditingEbillEndpoints(this IEndpointRouteBuilder app)
	{
		app.MapPut("/api/ebills/{ebillId:int}/editor-rights",
		async (int ebillId, UpdateEditorRightsDto dto, HttpContext http, ApplicationDbContext db) =>
		{
			var userId = http.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
			if (userId is null)
				return Results.Json(new { error = "Unauthorized" }, statusCode: 401);

			int userIdInt = int.Parse(userId);

			if (dto.Participants == null || dto.Participants.Count == 0)
				return Results.BadRequest(new { error = "Participants list cannot be empty" });

			if (dto.Participants.Select(x => x.ParticipantId).Distinct().Count() != dto.Participants.Count)
				return Results.BadRequest(new { error = "Duplicate ParticipantId values detected in request" });

			var ebill = await db.Ebills
				.Include(e => e.Participants)
				.FirstOrDefaultAsync(e => e.EbillId == ebillId);

			if (ebill is null)
				return Results.NotFound(new { error = "E-bill not found" });

			if (ebill.OrganizerId != userIdInt)
				return Results.Json(new { error = "Only organizer can update editor rights" }, statusCode: 403);

			var errors = new List<string>();

			foreach (var item in dto.Participants)
			{
				var participant = ebill.Participants
					.FirstOrDefault(p => p.ParticipantId == item.ParticipantId);

				if (participant == null)
				{
					errors.Add($"Participant {item.ParticipantId} not found");
					continue;
				}

				if (participant.UserId == ebill.OrganizerId)
				{
					errors.Add($"Cannot update rights for organizer (ParticipantId={item.ParticipantId})");
					continue;
				}

				if (participant.IsEditorRights == true)
				{
					errors.Add($"Participant {item.ParticipantId} already has editor rights and cannot be modified");
					continue;
				}
			}

			if (errors.Count > 0)
			{
				return Results.BadRequest(new
				{
					message = "Validation failed",
					errors
				});
			}

			foreach (var item in dto.Participants)
			{
				var participant = ebill.Participants
					.First(p => p.ParticipantId == item.ParticipantId);

				participant.IsEditorRights = item.IsEditorRights;
			}

			await db.SaveChangesAsync();

			return Results.Ok(new
			{
				message = "Editor rights updated successfully",
				updated = dto.Participants.Select(x => x.ParticipantId).ToList()
			});
		})
		.RequireAuthorization();
	
	app.MapPost("/api/ebills/{ebillId:int}/participants/add",
		async (int ebillId, AddParticipantsDto dto, HttpContext http, ApplicationDbContext db) =>
		{
			var userIdClaim = http.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
			if (userIdClaim is null)
				return Results.Json(new { error = "Unauthorized" }, statusCode: 401);

			int userId = int.Parse(userIdClaim);

			if (dto.UserIds == null || dto.UserIds.Count == 0)
				return Results.BadRequest(new { error = "UserIds list cannot be empty" });

			if (dto.UserIds.Count != dto.UserIds.Distinct().Count())
				return Results.BadRequest(new { error = "Duplicate user IDs detected in request" });

			var ebill = await db.Ebills
				.Include(e => e.Participants)
				.FirstOrDefaultAsync(e => e.EbillId == ebillId);

			if (ebill is null)
				return Results.NotFound(new { error = "E-bill not found" });

			var currentUser = ebill.Participants.FirstOrDefault(p => p.UserId == userId);
			if (ebill.OrganizerId != userId &&
				(currentUser == null || (!currentUser.IsAdminRights && !currentUser.IsEditorRights)))
				return Results.Json(new { error = "You do not have permission" }, statusCode: 403);

			var allowedContacts = await db.Contacts
				.Where(c => c.UserId == userId && c.Status == "active")
				.Select(c => c.FriendId)
				.ToListAsync();

			string scenario = ebill.Scenario.ToLower();

			List<int> actuallyAdded = new(); 
			List<int> alreadyParticipants = new();

			foreach (var uid in dto.UserIds)
			{

				if (!allowedContacts.Contains(uid))
					return Results.BadRequest(new { error = $"User {uid} is not in your contacts" });

				if (ebill.Participants.Any(p => p.UserId == uid))
				{
					alreadyParticipants.Add(uid);
					continue;
				}

				ebill.Participants.Add(new EbillParticipant
				{
					UserId = uid,
					AssignedAmount = 0,
					PaidAmount = 0,
					Balance = 0,
					IsAdminRights = false,
					IsEditorRights = false,
					PaymentStatus = "непогашений"
				});

				actuallyAdded.Add(uid);
                var addedUser = await db.Users.FindAsync(uid);
                if (addedUser == null)
                    return Results.BadRequest(new { error = $"User {uid} not found" });
                if (currentUser?.User == null)
                    return Results.BadRequest(new { error = "User record missing" });

                await EbillHistoryService.AddAsync(
                    db,
                    ebillId,
                    userId,
                    "added_participant",
                    $"{currentUser.User.FirstName} додав(-ла) {addedUser.FirstName} до чеку"
                );
            }

			if (actuallyAdded.Count == 0)
				return Results.BadRequest(new { error = "No participants were added", alreadyParticipants });

			if (scenario == "рівний розподіл" || scenario == "спільні витрати")
			{
				decimal equal = Math.Round(ebill.AmountOfDept / ebill.Participants.Count);

				foreach (var p in ebill.Participants)
				{
					p.AssignedAmount = equal;

					if (scenario == "спільні витрати")
						p.Balance = p.PaidAmount;
				}
			}

			foreach (var p in ebill.Participants)
			{
				if (p.Balance >= p.AssignedAmount)
					p.PaymentStatus = "погашений";
				else if (p.Balance == 0)
					p.PaymentStatus = "непогашений";
				else
					p.PaymentStatus = "частково погашений";
			}

			ebill.UpdatedAt = DateTime.UtcNow;
			await db.SaveChangesAsync();

			return Results.Ok(new
			{
				message = "Participants processed",
				added = actuallyAdded,
				alreadyParticipants
			});
		})

		.RequireAuthorization();
        app.MapPut("/api/ebills/{ebillId:int}/participants/update",
        async (int ebillId, UpdateParticipantDto dto, HttpContext http, ApplicationDbContext db) =>
        {
            var userIdClaim = http.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (userIdClaim is null)
                return Results.Json(new { error = "Unauthorized" }, statusCode: 401);

            int userId = int.Parse(userIdClaim);

            var ebill = await db.Ebills
                .Include(e => e.Participants)
                    .ThenInclude(p => p.User)
                .FirstOrDefaultAsync(e => e.EbillId == ebillId);

            if (ebill == null)
                return Results.NotFound(new { error = "E-bill not found" });

            var currentUser = ebill.Participants.FirstOrDefault(p => p.UserId == userId);
            if (ebill.OrganizerId != userId &&
                (currentUser == null || (!currentUser.IsAdminRights && !currentUser.IsEditorRights)))
                return Results.Json(new { error = "You do not have permission" }, statusCode: 403);

            string scenario = ebill.Scenario.ToLower();

            bool userMadeChanges = false;

            // --- Оновлення назви та опису ---
            if (!string.IsNullOrWhiteSpace(dto.Name) && dto.Name != ebill.Name)
            {
                ebill.Name = dto.Name;
                userMadeChanges = true;
            }

            if (!string.IsNullOrWhiteSpace(dto.Description) && dto.Description != ebill.Description)
            {
                ebill.Description = dto.Description;
                userMadeChanges = true;
            }

            // --- Оновлення суми боргу ---
            if (dto.AmountOfDept.HasValue)
            {
                if (dto.AmountOfDept.Value < 0)
                    return Results.BadRequest(new { error = "AmountOfDept must be non-negative" });

                ebill.AmountOfDept = dto.AmountOfDept.Value;
                userMadeChanges = true;

                if (scenario == "рівний розподіл" || scenario == "спільні витрати")
                {
                    decimal equal = Math.Round(ebill.AmountOfDept / ebill.Participants.Count);
                    foreach (var p in ebill.Participants)
                    {
                        p.AssignedAmount = equal;
                        if (scenario == "спільні витрати")
                            p.Balance = p.PaidAmount;
                    }
                }
                else if (scenario == "індивідуальні суми")
                {
                    decimal sumAssigned = ebill.Participants.Sum(p => p.AssignedAmount);
                    if (sumAssigned > ebill.AmountOfDept)
                    {
                        decimal diff = sumAssigned - ebill.AmountOfDept;
                        var lastPart = ebill.Participants.LastOrDefault();
                        if (lastPart != null)
                            lastPart.AssignedAmount = Math.Max(0, lastPart.AssignedAmount - diff);
                    }
                }
            }

            // --- Оновлення конкретного учасника ---
            if (dto.ParticipantId.HasValue)
            {
                var part = ebill.Participants.FirstOrDefault(p => p.ParticipantId == dto.ParticipantId.Value);
                if (part == null)
                    return Results.BadRequest(new { error = "Participant not found" });

                if (dto.AssignedAmount.HasValue)
                {
                    if (dto.AssignedAmount.Value < 0)
                        return Results.BadRequest(new { error = "AssignedAmount must be non-negative" });

                    part.AssignedAmount = dto.AssignedAmount.Value;
                    userMadeChanges = true;
                }

                if (dto.PaidAmount.HasValue && scenario == "спільні витрати")
                {
                    if (dto.PaidAmount.Value < 0)
                        return Results.BadRequest(new { error = "PaidAmount must be non-negative." });

                    decimal othersPaid = ebill.Participants
                        .Where(p => p.ParticipantId != part.ParticipantId)
                        .Sum(p => p.PaidAmount);

                    decimal maxAllowed = ebill.AmountOfDept - othersPaid;

                    if (dto.PaidAmount.Value > maxAllowed)
                        return Results.BadRequest(new
                        {
                            error = "Payment exceeds allowed amount.",
                            allowed = maxAllowed,
                            attempted = dto.PaidAmount.Value
                        });

                    part.PaidAmount = dto.PaidAmount.Value;
                    part.Balance = part.PaidAmount;
                    userMadeChanges = true;
                }
            }

            // --- Оновлення статусу платежів ---
            foreach (var p in ebill.Participants)
            {
                p.PaymentStatus = p.Balance >= p.AssignedAmount ? "погашений" :
                                  p.Balance == 0 ? "непогашений" : "частково погашений";
            }

            ebill.Status = ebill.Participants.All(p => p.PaymentStatus == "погашений") ? "закритий" : "активний";
            ebill.UpdatedAt = DateTime.UtcNow;

            // --- Лог змін ---
            if (userMadeChanges)
            {
                if (currentUser?.User == null)
                    return Results.BadRequest(new { error = "User record missing" });

                try
                {
                    await EbillHistoryService.AddAsync(
                        db,
                        ebillId,
                        userId,
                        "updated",
                        $"{currentUser.User.FirstName} оновив(-ла) чек"
                    );
                }
                catch (Exception ex)
                {
                    return Results.Problem($"Failed to log changes: {ex.Message}");
                }
            }

            try
            {
                await db.SaveChangesAsync();
            }
            catch (DbUpdateException dbEx)
            {
                return Results.Problem($"Database update failed: {dbEx.InnerException?.Message ?? dbEx.Message}");
            }
            catch (Exception ex)
            {
                return Results.Problem($"Unexpected error: {ex.Message}");
            }

            return Results.Ok(new { message = "E-bill updated successfully" });
        })
        .RequireAuthorization();
        app.MapDelete("/api/ebills/{ebillId:int}/participants/{participantId:int}/remove",
    async (int ebillId, int participantId, HttpContext http, ApplicationDbContext db) =>
    {
        var userIdClaim = http.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (userIdClaim == null)
            return Results.Json(new { error = "Unauthorized" }, statusCode: 401);

        int userId = int.Parse(userIdClaim);

        var ebill = await db.Ebills
            .Include(e => e.Participants)
                .ThenInclude(p => p.User)
            .FirstOrDefaultAsync(e => e.EbillId == ebillId);

        if (ebill == null)
            return Results.NotFound(new { error = "E-bill not found" });

        var currentUser = ebill.Participants.FirstOrDefault(p => p.UserId == userId);
        if (ebill.OrganizerId != userId &&
            (currentUser == null || (!currentUser.IsAdminRights )))
            return Results.Json(new { error = "You do not have permission" }, statusCode: 403);

        var participantToRemove = ebill.Participants.FirstOrDefault(p => p.ParticipantId == participantId);
        if (participantToRemove == null)
            return Results.NotFound(new { error = "Participant not found" });

        if (participantToRemove.UserId == ebill.OrganizerId)
            return Results.BadRequest(new { error = "Cannot remove organizer" });

        ebill.Participants.Remove(participantToRemove);

        if (ebill.Participants.Count > 0)
        {
            if (ebill.Scenario.ToLower() == "рівний розподіл" || ebill.Scenario.ToLower() == "спільні витрати")
            {
                decimal equal = Math.Round(ebill.AmountOfDept / ebill.Participants.Count);
                foreach (var p in ebill.Participants)
                {
                    p.AssignedAmount = equal;
                }
            }

            foreach (var p in ebill.Participants)
            {
                if (p.Balance >= p.AssignedAmount) p.PaymentStatus = "погашений";
                else if (p.Balance == 0) p.PaymentStatus = "непогашений";
                else p.PaymentStatus = "частково погашений";
            }

            ebill.Status = ebill.Participants.All(p => p.PaymentStatus == "погашений") ? "закритий" : "активний";
        }
        else
        {
            ebill.Status = "активний";
        }

        ebill.UpdatedAt = DateTime.UtcNow;
        if (currentUser?.User == null)
            return Results.BadRequest(new { error = "User record missing" });
        await EbillHistoryService.AddAsync(
            db,
            ebillId,
            userId,
            "removed_participant",
            $"{currentUser.User.FirstName} видалив(-ла) {participantToRemove.User.FirstName} з чеку"
        );

        await db.SaveChangesAsync();

        return Results.Ok(new
        {
            message = "Participant removed successfully",
            removedParticipantId = participantId
        });
    })
.RequireAuthorization();
    }
}
