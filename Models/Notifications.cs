namespace DeBillPay_Backend.Models;

public class Notification
{
    public int NotificationId { get; set; }
    public string Type { get; set; } = null!;
    public string MessageText { get; set; } = null!;
    public string Status { get; set; } = null!;
    public DateTime CreatedAt { get; set; }

    public int EbillId { get; set; }
    public Ebill Ebill { get; set; } = null!;

    public int UserId { get; set; }
    public User User { get; set; } = null!;
}