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
    public static class ContactEndpoints
    {
        public static void MapContactEndpoints(this WebApplication app)
        {

            app.MapGet("/api/contacts", async (HttpContext http, ApplicationDbContext db) =>
            {
                var userIdClaim = http.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (userIdClaim is null)
                    return Results.Unauthorized();

                int userId = int.Parse(userIdClaim);

                var contacts = await db.Contacts
                      .Where(c => c.UserId == userId)
                      .Include(c => c.Friend)
                      .Select(c => new
                      {
                          c.ContactId,
                          c.Status,
                          Friend = new
                          {
                              c.Friend.UserId,
                              c.Friend.FirstName,
                              c.Friend.LastName,
                              c.Friend.Email,
                              c.Friend.PhoneNumber
                          }
                      })
                      .ToListAsync();

                return Results.Ok(contacts);
            })
.RequireAuthorization();
            app.MapGet("/api/users/searchNewContact", async (string query, ApplicationDbContext db) =>
            {
                var normalized = query.Trim();

                var user = await db.Users
                    .Where(u => u.Email == normalized || u.PhoneNumber == normalized)
                    .Select(u => new
                    {
                        u.UserId,
                        u.FirstName,
                        u.LastName,
                        u.Email,
                        u.PhoneNumber
                    })
                    .FirstOrDefaultAsync();

                return user is null ? Results.NotFound("User not found") : Results.Ok(user);
            });
            app.MapGet("/api/users/searchFriend", async (string query, ApplicationDbContext db, HttpContext http) =>
            {
                var normalized = query.Trim();
                var userIdClaim = http.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (userIdClaim is null)
                    return Results.Unauthorized();

                int userId = int.Parse(userIdClaim);

                var contacts = await db.Contacts
                    .Where(c => c.UserId == userId &&
                           (c.Friend.FirstName.Contains(normalized) ||
                            c.Friend.LastName.Contains(normalized)))
                    .Include(c => c.Friend)
                    .Select(c => new
                    {
                        c.ContactId,
                        c.Status,
                        Friend = new
                        {
                            c.Friend.UserId,
                            c.Friend.FirstName,
                            c.Friend.LastName,
                            c.Friend.Email,
                            c.Friend.PhoneNumber
                        }
                    })
                    .ToListAsync();

                return Results.Ok(contacts);
            });
            app.MapPost("/api/contacts/invite", async (HttpContext http, ApplicationDbContext db, int receiverId) =>
            {
                var senderIdClaim = http.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (senderIdClaim == null)
                    return Results.Unauthorized();

                int senderId = int.Parse(senderIdClaim);
                var receiver = await db.Users.FindAsync(receiverId);
                if (receiver is null)
                    return Results.BadRequest("Receiver user does not exist");
                if (senderId == receiverId)
                    return Results.BadRequest("You cannot add yourself");

                bool exists = await db.Invitations.AnyAsync(i =>
                    i.SenderId == senderId && i.ReceiverId == receiverId && i.Status == "pending");

                if (exists)
                    return Results.Conflict("Invitation already sent");

                var invite = new Invitation
                {
                    Type = "contact",
                    Status = "pending",
                    CreatedAt = DateTime.UtcNow,
                    SenderId = senderId,
                    ReceiverId = receiverId
                };

                db.Invitations.Add(invite);

                var user = await db.Users.FindAsync(senderId);
                if (user is null)
                    return Results.BadRequest("Sender user record not found");

                await db.SaveChangesAsync();
               
                if (receiver != null && !string.IsNullOrWhiteSpace(receiver.Email))
                {
                    try
                    {
                        await EmailService.SendEmailAsync(
                       receiver.Email,
                       "Нове запрошення в друзі",
                       $"Привіт {receiver.FirstName},\n\nВи отримали запрошення в друзі від {user.FirstName} {user.LastName}.\n\nПерейдіть у додаток, щоб прийняти або відхилити запрошення.",
                       http.RequestServices.GetRequiredService<IConfiguration>()
                   );
                    }
                    catch
                    {

                    }
                }
                return Results.Ok("Invitation sent");
            })
 .RequireAuthorization();
            app.MapPost("/api/contacts/accept", async (HttpContext http, ApplicationDbContext db, int invitationId) =>
            {
                var userIdClaim = http.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (userIdClaim is null)
                    return Results.Unauthorized();

                int userId = int.Parse(userIdClaim);

                var invite = await db.Invitations
                    .FirstOrDefaultAsync(i => i.InvitationId == invitationId && i.ReceiverId == userId);

                if (invite == null)
                    return Results.NotFound("Invitation not found");

                if (invite.Status != "pending")
                    return Results.BadRequest("Invitation already processed");

                invite.Status = "accepted";

                db.Contacts.Add(new Contact
                {
                    Status = "active",
                    UserId = invite.SenderId,
                    FriendId = invite.ReceiverId
                });

                db.Contacts.Add(new Contact
                {
                    Status = "active",
                    UserId = invite.ReceiverId,
                    FriendId = invite.SenderId
                });

                await db.SaveChangesAsync();

                return Results.Ok("Contact added");
            })
.RequireAuthorization();
            app.MapPost("/api/contacts/reject", async (HttpContext http, ApplicationDbContext db, int invitationId) =>
            {
                var userIdClaim = http.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (userIdClaim is null)
                    return Results.Unauthorized();

                int userId = int.Parse(userIdClaim);

                var invite = await db.Invitations
                    .FirstOrDefaultAsync(i => i.InvitationId == invitationId && i.ReceiverId == userId);

                if (invite == null)
                    return Results.NotFound();

                invite.Status = "rejected";
                await db.SaveChangesAsync();

                return Results.Ok("Invitation rejected");
            })
.RequireAuthorization();
            app.MapDelete("/api/contacts/delete", async (HttpContext http, ApplicationDbContext db, int friendId) =>
            {
                var userIdClaim = http.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (userIdClaim is null)
                    return Results.Unauthorized();

                int userId = int.Parse(userIdClaim);

                var contacts = await db.Contacts
                    .Where(c =>
                        (c.UserId == userId && c.FriendId == friendId) ||
                        (c.UserId == friendId && c.FriendId == userId))
                    .ToListAsync();

                if (!contacts.Any())
                    return Results.NotFound("Contact does not exist");

                db.Contacts.RemoveRange(contacts);
                await db.SaveChangesAsync();

                return Results.Ok("Contact deleted");
            })
.RequireAuthorization();

        }
    }
}
