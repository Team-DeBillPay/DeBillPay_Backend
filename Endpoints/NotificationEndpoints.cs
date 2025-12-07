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
                        Id = n.NotificationId, 
                        Type = n.Type,
                        Message = n.MessageText,
                        Status = n.Status,
                        CreatedAt = n.CreatedAt
                    })
                    .OrderByDescending(n => n.CreatedAt)
                    .ToListAsync();

                return Results.Ok(notifications);
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

