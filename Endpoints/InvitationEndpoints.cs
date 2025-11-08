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

namespace DeBillPay_Backend.Endpoints
{
    public static class InvitationEndpoints
    {
        public static void MapInvitationEndpoints(this WebApplication app)
        {


            app.MapGet("/api/users/invitationsContacts", async (HttpContext http, ApplicationDbContext db) =>
            {
                var userIdClaim = http.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                int userId = int.Parse(userIdClaim);
                if (userIdClaim == null)
                    return Results.Unauthorized();

                var invitations = await db.Invitations
                       .Where(i => i.ReceiverId == userId && i.Status == "pending")
                       .Include(i => i.Sender)
                      .Select(i => new
                      {
                          i.InvitationId,
                          i.Type,
                          i.Status,
                          i.CreatedAt,
                          Sender = new
                          {
                              i.Sender.UserId,
                              i.Sender.FirstName,
                              i.Sender.LastName,
                              i.Sender.Email,
                              i.Sender.PhoneNumber
                          }
                      })
        .ToListAsync();

                return Results.Ok(invitations);
            })
.RequireAuthorization();


        }
    }
}

