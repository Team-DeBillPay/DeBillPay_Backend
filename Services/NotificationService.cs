using DeBillPay_Backend.Data;
using DeBillPay_Backend.Models;

namespace DeBillPay_Backend.Services;

public static class NotificationService
{
    public static async Task CreateAsync(
        ApplicationDbContext db,
        int userId,
        string type,
        string message,
        int? ebillId = null)
    {
        var notif = new Notification
        {
            UserId = userId,
            Type = type,
            MessageText = message,
            Status = "unread",
            CreatedAt = DateTime.UtcNow.AddHours(2),
            EbillId = ebillId
        };

        db.Notifications.Add(notif);
        await db.SaveChangesAsync();
    }
}