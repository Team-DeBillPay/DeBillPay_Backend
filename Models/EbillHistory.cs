namespace DeBillPay_Backend.Models;
public class EbillHistory
{
    public int EbillHistoryId { get; set; }
    public int EbillId { get; set; }
    public int UserId { get; set; }

    public required string ActionType { get; set; }
    public required string Message { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow.AddHours(2);
}