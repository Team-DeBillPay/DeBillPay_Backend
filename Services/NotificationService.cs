using DeBillPay_Backend.Data;
using Microsoft.EntityFrameworkCore;
using DeBillPay_Backend.Models;

namespace DeBillPay_Backend.Services;

public class NotificationService
{
    private readonly ApplicationDbContext _db;

    public NotificationService(ApplicationDbContext db)
    {
        _db = db;
    }

    public async Task CreateAsync(
        int userId,
        string type,
        string message,
        int? ebillId = null,
        int? groupId = null)
    {
        if (ebillId.HasValue &&
            !await _db.Ebills.AnyAsync(e => e.EbillId == ebillId.Value))
        {
            throw new Exception($"Invalid EbillId: {ebillId}");
        }

        if (groupId.HasValue &&
            !await _db.Groups.AnyAsync(g => g.GroupId == groupId.Value))
        {
            throw new Exception($"Invalid GroupId: {groupId}");
        }

        var notif = new Notification
        {
            UserId = userId,
            Type = type,
            MessageText = message,
            Status = "unread",
            CreatedAt = DateTime.UtcNow,
            EbillId = ebillId,
            GroupId = groupId
        };

        _db.Notifications.Add(notif);
        await _db.SaveChangesAsync();
    }
}
