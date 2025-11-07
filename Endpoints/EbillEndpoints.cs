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


        app.MapPost("/api/ebills/create", async (HttpContext http, ApplicationDbContext db, CreateEbillDto dto) =>
        {
            var userIdClaim = http.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (userIdClaim == null)
                return Results.Unauthorized();

            int organizerId = int.Parse(userIdClaim);

            var ebill = new Ebill
            {
                Name = dto.Name,
                Currency = dto.Currency,
                AmountOfDept = dto.AmountOfDept,
                Description = dto.Description,
                Scenario = dto.Scenario,
                Status = "active",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                OrganizerId = organizerId
            };

            foreach (var participantId in dto.ParticipantIds.Distinct())
            {
                ebill.Participants.Add(new EbillParticipant
                {
                    UserId = participantId,
                    PaymentStatus = "pending",
                    IsAdminRights = false
                });
            }

            db.Ebills.Add(ebill);
            await db.SaveChangesAsync();

            return Results.Ok(new
            {
                ebill.EbillId,
                ebill.Name,
                ebill.Currency,
                ebill.Status,
                Participants = ebill.Participants.Count
            });
        })
.RequireAuthorization();
    }
}
