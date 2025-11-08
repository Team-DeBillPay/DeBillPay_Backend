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

public static class EbillEndpoints
{
    public static void MapEbillEndpoints(this IEndpointRouteBuilder app)
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
                        p.PaymentStatus,
                        p.AssignedAmount,
                        p.PaidAmount,
                        p.Balance,
                        p.IsAdminRights
                    })
                })
                .ToListAsync();

            return Results.Ok(ebills);
        });

        app.MapPost("/api/ebills/create", async (HttpContext http, ApplicationDbContext db, CreateEbillDto dto) =>
        {
            var userIdClaim = http.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (userIdClaim == null)
                return Results.Unauthorized();

            int organizerId = int.Parse(userIdClaim);
            var organizer = await db.Users.FindAsync(organizerId);
            if (organizer == null)
                return Results.NotFound("Organizer not found.");
            var allowedContacts = await db.Contacts
       .Where(c => c.UserId == organizerId && c.Status == "active")
       .Select(c => c.FriendId)
       .ToListAsync();

            var invalidParticipants = dto.Participants
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

            var participants = dto.Participants.DistinctBy(p => p.UserId).ToList();

            switch (dto.Scenario.ToLower())
            {
                // Рівний розподіл 
                case "рівний розподіл":
                    {
                        if (participants.Count == 0)
                            return Results.BadRequest("No participants provided.");

                        var share = dto.AmountOfDept / participants.Count;

                        foreach (var p in participants)
                        {
                            ebill.Participants.Add(new EbillParticipant
                            {
                                UserId = p.UserId,
                                AssignedAmount = share,
                                PaidAmount = 0,
                                Balance = share,
                                PaymentStatus = "pending",
                                IsAdminRights = false
                            });
                        }

                        ebill.Participants.Add(new EbillParticipant
                        {
                            UserId = organizerId,
                            AssignedAmount = 0,
                            PaidAmount = dto.AmountOfDept,
                            Balance = 0,
                            PaymentStatus = "paid",
                            IsAdminRights = true
                        });

                        break;
                    }

                // Індивідуальні суми (організатор не платить)
                case "індивідуальні суми":
                    {
                        foreach (var p in participants)
                        {
                            var assigned = p.Amount ?? 0;
                            var paid = p.PaidAmount;
                            var balance = paid - assigned;

                            ebill.Participants.Add(new EbillParticipant
                            {
                                UserId = p.UserId,
                                AssignedAmount = assigned,
                                PaidAmount = paid,
                                Balance = balance,
                                PaymentStatus = balance >= 0 ? "paid" : "pending",
                                IsAdminRights = p.UserId == organizerId
                            });
                        }

                        break;
                    }
                // Спільні витрати (організатор не платить)
                case "спільні витрати":
                    {
                        if (participants.Count == 0)
                            return Results.BadRequest("No participants provided.");

                        decimal totalAmount = dto.AmountOfDept;

                        decimal share = totalAmount / participants.Count;

                        foreach (var p in participants)
                        {
                            decimal balance = p.PaidAmount - share;

                            ebill.Participants.Add(new EbillParticipant
                            {
                                UserId = p.UserId,
                                AssignedAmount = share,        
                                PaidAmount = p.PaidAmount,
                                Balance = balance,
                                PaymentStatus = p.PaidAmount >= share ? "paid" : "pending",
                                IsAdminRights = p.UserId == organizerId
                            });
                        }

                        break;
                    }
                default:
                    return Results.BadRequest("Unknown calculation scenario.");
            }

            if (ebill.Participants.All(p => p.PaymentStatus == "paid"))
                ebill.Status = "повністю оплачений";
            else if (ebill.Participants.Any(p => p.PaymentStatus == "paid"))
                ebill.Status = "частково оплачений";
            else
                ebill.Status = "активний";

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
                    p.AssignedAmount,
                    p.PaidAmount,
                    p.Balance,
                    p.PaymentStatus,
                    p.IsAdminRights
                })
            });
        })
        .RequireAuthorization();
    }
}