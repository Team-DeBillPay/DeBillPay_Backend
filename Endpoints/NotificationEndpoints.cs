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
    public static class NotificationEndpoints
    {
        public static void MapNotificationEndpoints(this WebApplication app)
        {

            app.MapGet("/api/notifications/all", async (HttpContext http, ApplicationDbContext db) =>
            {
                var userIdClaim = http.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (userIdClaim == null)
                    return Results.Unauthorized();

                int userId = int.Parse(userIdClaim);

                var notifications = await db.Notifications
                    .Where(n => n.UserId == userId)
                    .Select(n => new
                    {
                        Type = n.Type,              
                        Message = n.MessageText,
                        Status = n.Status,
                        CreatedAt = n.CreatedAt
                    })
                    .ToListAsync();

                var invitations = await db.Invitations
                    .Where(i => i.ReceiverId == userId && i.Status == "pending")
                    .Include(i => i.Sender)
                    .Select(i => new
                    {
                        Type = "friend_invitation",
                        Message = $"Запрошення від {i.Sender.FirstName} {i.Sender.LastName}",
                        Status = i.Status,
                        CreatedAt = i.CreatedAt
                    })
                    .ToListAsync();

                var all = notifications
                    .Concat(invitations)
                    .OrderByDescending(x => x.CreatedAt)
                    .ToList();

                return Results.Ok(all);
            })
 .RequireAuthorization();
            app.MapGet("/api/notifications/{notificationId:int}", 
    async (int notificationId, HttpContext http, ApplicationDbContext db) =>
{
    var userIdClaim = http.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
    if (userIdClaim == null)
        return Results.Unauthorized();

    int userId = int.Parse(userIdClaim);

    var notification = await db.Notifications
        .Where(n => n.NotificationId == notificationId && n.UserId == userId)
        .Select(n => new
        {
            n.NotificationId,
            n.Type,
            Message = n.MessageText,
            n.Status,
            n.CreatedAt,
            n.EbillId
        })
        .FirstOrDefaultAsync();

    if (notification == null)
        return Results.NotFound(new { error = "Notification not found" });

    return Results.Ok(notification);
})
.RequireAuthorization();
            app.MapPut("/api/notifications/mark-read/{notificationId:int}",
    async (int notificationId, HttpContext http, ApplicationDbContext db) =>
    {
        var userIdClaim = http.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (userIdClaim is null)
            return Results.Unauthorized();

        int userId = int.Parse(userIdClaim);

        var notification = await db.Notifications
            .FirstOrDefaultAsync(n => n.NotificationId == notificationId && n.UserId == userId);

        if (notification is null)
            return Results.NotFound(new { error = "Notification not found" });

        if (notification.Status == "read")
        {
            return Results.Ok(new { message = "Already read" });
        }

        notification.Status = "read";
        await db.SaveChangesAsync();

        return Results.Ok(new { message = "Notification marked as read" });
    })
.RequireAuthorization();

        }
    }
}

