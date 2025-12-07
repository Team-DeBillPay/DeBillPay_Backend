using DeBillPay_Backend.Data;
using DeBillPay_Backend.Models;
using Microsoft.EntityFrameworkCore;

namespace DeBillPay_Backend.Services;

public static class NotificationService
{
    public static async Task CreateAsync(
        ApplicationDbContext db,
        int userId,
        string type,
        string message,
        int? ebillId = null,
        int? groupId = null)
    {
        if (ebillId.HasValue)
        {
            var exists = await db.Ebills.AnyAsync(e => e.EbillId == ebillId.Value);
            if (!exists)
                throw new Exception($"Invalid EbillId: {ebillId}");
        }

        if (groupId.HasValue)
        {
            var exists = await db.Groups.AnyAsync(g => g.GroupId == groupId.Value);
            if (!exists)
                throw new Exception($"Invalid GroupId: {groupId}");
        }
        var notif = new Notification
        {
            UserId = userId,
            Type = type,
            MessageText = message,
            Status = "unread",
            CreatedAt = DateTime.UtcNow.AddHours(2),
            EbillId = ebillId,
            GroupId = groupId
        };

        db.Notifications.Add(notif);
        await db.SaveChangesAsync();
    }
}