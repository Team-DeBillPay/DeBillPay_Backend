using System.Text.Json;
using DeBillPay_Backend.Data;
using DeBillPay_Backend.Models;
namespace DeBillPay_Backend.Services
{
    public static class EbillHistoryService
    {
        public static async Task AddAsync(
            ApplicationDbContext db,
            int ebillId,
            int userId,
            string actionType,
            string message)
        {
            var entry = new EbillHistory
            {
                EbillId = ebillId,
                UserId = userId,
                ActionType = actionType,
                Message = message
            };

            db.EbillHistories.Add(entry);
            await db.SaveChangesAsync();
        }
    }
}