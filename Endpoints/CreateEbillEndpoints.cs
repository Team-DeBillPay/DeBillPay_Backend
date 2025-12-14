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

public static class CreateEbillEndpoints
{
    public static void MapCreateEbillEndpoints(this IEndpointRouteBuilder app)
    {

        app.MapGet("/api/ebills", async (HttpContext http, ApplicationDbContext db) =>
        {
            var userIdClaim = http.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (userIdClaim is null) return Results.Unauthorized();

            int userId = int.Parse(userIdClaim);

            var ebills = await db.Ebills
                .Where(e => e.OrganizerId == userId || e.Participants.Any(p => p.UserId == userId))
                .Select(e => new
                {
                    e.EbillId,
                    e.Name,
                    e.Currency,
                    e.AmountOfDept,
                    e.Description,
                    e.Scenario,
                    e.Status,
                    e.CreatedAt,
                    e.UpdatedAt,
                    Participants = e.Participants.Select(p => new
                    {
                        p.UserId,
                        p.ParticipantId,
                        p.AssignedAmount,
                        p.PaidAmount,
                        p.Balance,
                        p.PaymentStatus,
                        p.IsAdminRights,
                        p.IsEditorRights
                    })
                })
                .ToListAsync();

            return Results.Ok(ebills);
        });

        app.MapGet("/api/ebills/{id:int}", async (int id, HttpContext http, ApplicationDbContext db) =>
        {
            var userIdClaim = http.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (userIdClaim is null) return Results.Unauthorized();

            int userId = int.Parse(userIdClaim);

            var ebill = await db.Ebills
                .Where(e => e.EbillId == id && (e.OrganizerId == userId || e.Participants.Any(p => p.UserId == userId)))
                .Select(e => new
                {
                    e.EbillId,
                    e.Name,
                    e.Currency,
                    e.AmountOfDept,
                    e.Description,
                    e.Scenario,
                    e.Status,
                    e.CreatedAt,
                    e.UpdatedAt,
                    Participants = e.Participants.Select(p => new
                    {
                        p.UserId,
                        p.ParticipantId,
                    p.AssignedAmount,
                        p.PaidAmount,
                        p.Balance,
                        p.PaymentStatus,
                        p.IsAdminRights,
                        p.IsEditorRights
                    })
                })
                .FirstOrDefaultAsync();

            if (ebill == null)
                return Results.NotFound(new { message = "Чек не знайдено або у вас немає доступу" });

            return Results.Ok(ebill);
        });

        app.MapPost("/api/ebills/create", async (
     HttpContext http,
     ApplicationDbContext db,
     NotificationService notificationService,   // <- додати DI
     EbillHistoryService ebillHistoryService,   // <- додати DI
     CreateEbillDto dto) =>
        {
            var userIdClaim = http.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (userIdClaim == null)
                return Results.Unauthorized();
            int organizerId = int.Parse(userIdClaim);
            var organizer = await db.Users.FindAsync(organizerId);
            if (organizer == null)
                return Results.NotFound("Organizer not found.");
            if (dto == null)
                return Results.BadRequest("Request body is empty.");
            if (dto.GroupId.HasValue)
{
    // 1. Перевіряємо чи існує група та чи вона належить юзеру
    var group = await db.Groups
        .Include(g => g.Members)
        .FirstOrDefaultAsync(g => g.GroupId == dto.GroupId && g.UserId == organizerId);

    if (group == null)
        return Results.BadRequest(new { error = "Group not found or you do not own this group." });

    // 2. Перетворюємо членів групи у список учасників
    dto.Participants = group.Members
        .Select(m => new ParticipantAmountDto
        {
            UserId = m.MemberId,
            Amount = 0,
            PaidAmount = 0
        })
        .ToList();
}


            if (string.IsNullOrWhiteSpace(dto.Name))
                return Results.BadRequest("Name is required.");
            if (string.IsNullOrWhiteSpace(dto.Currency))
                return Results.BadRequest("Currency is required.");

            string[] allowedCurrencies = { "UAH", "USD", "EUR" };

            if (!allowedCurrencies.Contains(dto.Currency.ToUpper()))
                return Results.BadRequest("Invalid currency. Allowed: UAH, USD, EUR.");

            if (dto.Currency.Length != 3)
                return Results.BadRequest("Currency must be a 3-letter ISO code.");

            if (dto.AmountOfDept <= 0)
                return Results.BadRequest("AmountOfDept must be greater than zero.");

            if (dto.Participants == null || dto.Participants.Count == 0)
                return Results.BadRequest("Participants list cannot be empty.");


            var participants = dto.Participants.DistinctBy(p => p.UserId).ToList();

            if (participants.Count != dto.Participants.Count)
                return Results.BadRequest("Duplicate UserId in participants.");

            string[] allowedScenarios =
            {
    "рівний розподіл",
    "індивідуальні суми",
    "спільні витрати"
};

            if (!allowedScenarios.Contains(dto.Scenario.ToLower()))
                return Results.BadRequest("Invalid scenario. Allowed: рівний розподіл, індивідуальні суми, спільні витрати");

            var participantIds = participants.Select(p => p.UserId).ToList();

            var existingUserIds = await db.Users
                .Where(u => participantIds.Contains(u.UserId))
                .Select(u => u.UserId)
                .ToListAsync();
           
                var missingUsers = participantIds.Except(existingUserIds).ToList();

            if (missingUsers.Any())
                return Results.BadRequest($"These participants do not exist: {string.Join(", ", missingUsers)}");

            if (dto.Scenario.ToLower() == "індивідуальні суми")
            {
                decimal totalAssigned = participants.Sum(x => x.Amount ?? 0);
                dto.AmountOfDept = totalAssigned; 
            }

            if (dto.Scenario.ToLower() == "спільні витрати")
            {
                decimal totalPaid = participants.Sum(x => x.PaidAmount);

                dto.AmountOfDept = totalPaid;
            }

            if (dto.AmountOfDept < 0)
                return Results.BadRequest("AmountOfDept cannot be negative.");

            if (participants.Any(x => x.PaidAmount > dto.AmountOfDept))
                return Results.BadRequest("PaidAmount cannot exceed AmountOfDept.");

            foreach (var p in participants)
            {
                if (p.Amount < 0)
                    return Results.BadRequest($"Assigned amount for user {p.UserId} cannot be negative.");

                if (p.PaidAmount < 0)
                    return Results.BadRequest($"Paid amount for user {p.UserId} cannot be negative.");
            }

            decimal totalAssignedAmount = participants.Sum(x => x.Amount ?? 0);
            if (totalAssignedAmount > dto.AmountOfDept)
                return Results.BadRequest($"Total AssignedAmount ({totalAssignedAmount}) cannot exceed AmountOfDept ({dto.AmountOfDept}).");

            var allowedContacts = await db.Contacts
                .Where(c => c.UserId == organizerId && c.Status == "active")
                .Select(c => c.FriendId)
                .ToListAsync();

            var invalidParticipants = participants
                .Where(p => p.UserId != organizerId && !allowedContacts.Contains(p.UserId))
                .ToList();

            if (invalidParticipants.Any())
            {
                var invalidIds = string.Join(", ", invalidParticipants.Select(p => p.UserId));
                return Results.BadRequest($"These users are not in your contact list: {invalidIds}");
            }

            var ebill = new Ebill
            {
                Name = dto.Name,
                Currency = dto.Currency,
                AmountOfDept = dto.AmountOfDept,
                Description = dto.Description,
                Scenario = dto.Scenario.ToLower(),
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                OrganizerId = organizerId
            };


            switch (dto.Scenario.ToLower())
            {
                case "рівний розподіл":
                    {
                        if (participants.Count == 0)
                            return Results.BadRequest("No participants provided.");
                        var amount = dto.AmountOfDept;
                        var participants1 = participants.Count+1;
                        var share = dto.AmountOfDept / participants1;

                        foreach (var p in participants)
                        {
                            ebill.Participants.Add(new EbillParticipant
                            {
                                UserId = p.UserId,
                                AssignedAmount = Math.Round(share),
                                PaidAmount = 0,
                                Balance = 0,
                                IsAdminRights = false,
                                IsEditorRights = false
                            });
                        }

                        ebill.Participants.Add(new EbillParticipant
                        {
                            UserId = organizerId,
                            AssignedAmount = Math.Round(share),
                            PaidAmount = 0,
                            Balance = 0,
                            IsAdminRights = true,
                            IsEditorRights = true
                        });

                        break;
                    }

                case "індивідуальні суми":
                    {
                        decimal totalAssigned = participants.Sum(p => p.Amount ?? 0);
                        ebill.AmountOfDept = totalAssigned; 

                        foreach (var p in participants)
                        {
                            ebill.Participants.Add(new EbillParticipant
                            {
                                UserId = p.UserId,
                                AssignedAmount = Math.Round(p.Amount ?? 0),
                                PaidAmount = p.PaidAmount,
                                Balance = p.PaidAmount,
                                IsAdminRights = p.UserId == organizerId,
                                IsEditorRights = p.UserId == organizerId
                            });
                        }

                        break;
                    }

                case "спільні витрати":
                    {
                        if (participants.Count == 0)
                            return Results.BadRequest("No participants provided.");

                        decimal totalPaid = participants.Sum(x => x.PaidAmount);
                        decimal share = participants.Count > 0
                            ? totalPaid / participants.Count
                            : 0;

                        ebill.AmountOfDept = totalPaid;

                        foreach (var p in participants)
                        {
                            ebill.Participants.Add(new EbillParticipant
                            {
                                UserId = p.UserId,
                                AssignedAmount = Math.Round(share),
                                PaidAmount = p.PaidAmount,
                                Balance = p.PaidAmount,
                                IsAdminRights = p.UserId == organizerId,
                                IsEditorRights = p.UserId == organizerId
                            });
                        }
                        break;
                    }
                default:
                    return Results.BadRequest("Unknown calculation scenario.");
            }


                foreach (var p in ebill.Participants)
                {
                if (ebill.Scenario == "індивідуальні суми")
                {
                    if (p.IsAdminRights)
                    {
                        p.PaymentStatus = "погашений";
                        continue;
                    }
                }
                if (p.Balance >= p.AssignedAmount && p.AssignedAmount > 0)
                        p.PaymentStatus = "погашений";
                    else if (p.Balance == 0)
                        p.PaymentStatus = "непогашений";
                    else if (p.Balance > 0 && p.Balance < p.AssignedAmount)
                        p.PaymentStatus = "частково погашений";
                    else
                        p.PaymentStatus = "непогашений";
                }
          

            ebill.Status = ebill.Participants.All(p => p.PaymentStatus == "погашений")
                            ? "закритий"
                            : "активний";
            db.Ebills.Add(ebill);
            await db.SaveChangesAsync();
            

            return Results.Ok(new
            {
                ebill.EbillId,
                ebill.Name,
                ebill.Currency,
                ebill.Status,
                Scenario = ebill.Scenario,
                Participants = ebill.Participants.Select(p => new
                {
                    p.UserId,
                    p.ParticipantId,
                    p.AssignedAmount,
                    p.PaidAmount,
                    p.Balance,
                    p.PaymentStatus,
                    p.IsAdminRights,
                     p.IsEditorRights
                })
            });
        })
        .RequireAuthorization();

        app.MapDelete("/api/ebills/delete{id:int}", async (int id, HttpContext http, ApplicationDbContext db) =>
        {
            var userIdClaim = http.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (userIdClaim is null)
                return Results.Json(
    new { message = "Unauthorized" },
    statusCode: StatusCodes.Status401Unauthorized
);

            int userId = int.Parse(userIdClaim);

            var ebill = await db.Ebills
                .Include(e => e.Participants)
                .Include(e => e.Payments)
                .Include(e => e.Comments)
                .Include(e => e.Invitations)
                .Include(e => e.Notifications)
                .FirstOrDefaultAsync(e => e.EbillId == id);

            if (ebill == null)
                return Results.NotFound(new { message = "E-bill not found" });

            if (ebill.OrganizerId != userId)
                return Results.Json(
                    new { message = "You do not have permission to delete" },
                    statusCode: StatusCodes.Status403Forbidden
                );

            db.Ebills.Remove(ebill);
            await db.SaveChangesAsync();

            return Results.Ok(new { message = "E-bill successful delete" });
        })
.RequireAuthorization();
    }
}
