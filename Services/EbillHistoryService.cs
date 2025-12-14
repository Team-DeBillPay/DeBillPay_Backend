using DeBillPay_Backend.Data;
using Microsoft.EntityFrameworkCore;
using DeBillPay_Backend.Models;

namespace DeBillPay_Backend.Services;

public class EbillHistoryService
{
    private readonly ApplicationDbContext _db;

    public EbillHistoryService(ApplicationDbContext db)
    {
        _db = db;
    }

    public async Task AddAsync(
        int ebillId,
        int userId,
        string actionType,
        string message)
    {
        _db.EbillHistories.Add(new EbillHistory
        {
            EbillId = ebillId,
            UserId = userId,
            ActionType = actionType,
            Message = message
        });

        await _db.SaveChangesAsync();
    }
}