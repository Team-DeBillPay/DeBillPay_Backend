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
    private static void RecalculateEbill(Ebill ebill)
    {
        string s = ebill.Scenario.ToLower();

        foreach (var p in ebill.Participants)
            p.Balance = p.PaidAmount;

        if (s == "спільні витрати")
        {
            ebill.AmountOfDept = ebill.Participants.Sum(p => p.PaidAmount);

            decimal equal = Math.Round(ebill.AmountOfDept / ebill.Participants.Count);

            foreach (var p in ebill.Participants)
                p.AssignedAmount = equal;
        }
        else if (s == "рівний розподіл")
        {
            decimal equal = Math.Round(ebill.AmountOfDept / ebill.Participants.Count);

            foreach (var p in ebill.Participants)
                p.AssignedAmount = equal;
        }
        else if (s == "індивідуальні суми")
        {
            ebill.AmountOfDept = ebill.Participants.Sum(p => p.AssignedAmount);
        }

        foreach (var p in ebill.Participants)
        {
            p.PaymentStatus =
                p.Balance >= p.AssignedAmount ? "погашений" :
                p.Balance == 0 ? "непогашений" :
                "частково погашений";
        }

        ebill.Status = ebill.Participants.All(p => p.PaymentStatus == "погашений")
            ? "закритий"
            : "активний";

        ebill.UpdatedAt = DateTime.UtcNow;
    }

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
        .ThenInclude(p => p.User)
    .FirstOrDefaultAsync(e => e.EbillId == ebillId);

            if (ebill is null)
				return Results.NotFound(new { error = "E-bill not found" });

            var currentUser = ebill.Participants.FirstOrDefault(p => p.UserId == userId);
            if (ebill.OrganizerId != userId  &&
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
                var actor = await db.Users.FindAsync(userId);

                if (actor == null)
                    return Results.BadRequest(new { error = "User record missing" });

                
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

            List<string> changedFields = new();
            bool userMadeChanges = false;

            if (dto.Name != null && dto.Name != ebill.Name)
            {
                if (string.IsNullOrWhiteSpace(dto.Name))
                    return Results.BadRequest(new { error = "Name cannot be empty." });

                changedFields.Add($"назву (\"{ebill.Name}\" → \"{dto.Name}\")");
                ebill.Name = dto.Name;
                userMadeChanges = true;
            }

            if (dto.Description != null && dto.Description != ebill.Description)
            {
                if (string.IsNullOrWhiteSpace(dto.Description))
                    return Results.BadRequest(new { error = "Description cannot be empty." });

                changedFields.Add($"опис (\"{ebill.Description}\" → \"{dto.Description}\")");
                ebill.Description = dto.Description;
                userMadeChanges = true;
            }


            if (dto.AmountOfDept.HasValue)
            {
                if (scenario == "індивідуальні суми")
                    return Results.BadRequest(new { error = "AmountOfDept is auto-calculated in this scenario." });

                if (dto.AmountOfDept.Value < 0)
                    return Results.BadRequest(new { error = "AmountOfDept must be non-negative" });

                if (dto.AmountOfDept.Value != ebill.AmountOfDept)
                {
                    changedFields.Add($"загальну суму ({ebill.AmountOfDept} → {dto.AmountOfDept.Value})");
                    ebill.AmountOfDept = dto.AmountOfDept.Value;
                    userMadeChanges = true;
                }
            }

            if (dto.ParticipantId.HasValue)
            {
                var part = ebill.Participants.FirstOrDefault(p => p.ParticipantId == dto.ParticipantId.Value);
                if (part == null)
                    return Results.BadRequest(new { error = "Participant not found" });

                if (dto.AssignedAmount.HasValue)
                {
                    if (scenario is "спільні витрати" or "рівний розподіл")
                        return Results.BadRequest(new { error = "AssignedAmount cannot be manually edited in this scenario." });

                    if (dto.AssignedAmount.Value < 0)
                        return Results.BadRequest(new { error = "AssignedAmount must be non-negative" });

                    changedFields.Add(
                        $"суму, яку має сплатити {part.User.FirstName} ({part.AssignedAmount} → {dto.AssignedAmount.Value})"
                    );

                    part.AssignedAmount = dto.AssignedAmount.Value;
                    userMadeChanges = true;

                    if (scenario == "індивідуальні суми")
                        ebill.AmountOfDept = ebill.Participants.Sum(p => p.AssignedAmount);
                }

                if (dto.PaidAmount.HasValue)
                {
                    if (scenario is "рівний розподіл" or "індивідуальні суми")
                        return Results.BadRequest(new { error = "PaidAmount cannot be manually edited in this scenario." });

                    if (dto.PaidAmount.Value < 0)
                        return Results.BadRequest(new { error = "PaidAmount must be non-negative." });

                    changedFields.Add(
                        $"суму, яку витратив {part.User.FirstName} ({part.PaidAmount} → {dto.PaidAmount.Value})"
                    );

                    part.PaidAmount = dto.PaidAmount.Value;
                    part.Balance = part.PaidAmount;
                    userMadeChanges = true;
                }
            }

            RecalculateEbill(ebill);

            if (changedFields.Any())
            {
                var actor = await db.Users.FirstAsync(u => u.UserId == userId);

                foreach (var f in changedFields)
                {
                    await EbillHistoryService.AddAsync(
                        db, ebillId, userId, "updated",
                        $"{actor.FirstName} {actor.LastName} оновив(-ла) {f}"
                    );
                }
            }

            await db.SaveChangesAsync();
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
        User? actorUser = null;

        if (currentUser != null)
        {
            actorUser = currentUser.User;
        }
        else
        {
            // Якщо діє організатор — беремо його з Users
            if (ebill.OrganizerId == userId)
                actorUser = await db.Users.FirstOrDefaultAsync(u => u.UserId == userId);
        }

        if (actorUser == null)
            return Results.BadRequest(new { error = "User record missing" });

        // Логування
        await EbillHistoryService.AddAsync(
            db,
            ebillId,
            userId,
            "removed_participant",
            $"{actorUser.FirstName} видалив(-ла) {participantToRemove.User.FirstName} з чеку"
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
