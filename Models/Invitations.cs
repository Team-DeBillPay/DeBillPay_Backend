namespace DeBillPay_Backend.Models;

public class Invitation
{
    public int InvitationId { get; set; }
    public string Type { get; set; } = null!;
    public string Status { get; set; } = null!;
    public DateTime CreatedAt { get; set; }

    // FK
    public int EbillId { get; set; }
    public Ebill Ebill { get; set; } = null!;

    public int SenderId { get; set; }
    public User Sender { get; set; } = null!;

    public int ReceiverId { get; set; }
    public User Receiver { get; set; } = null!;
}