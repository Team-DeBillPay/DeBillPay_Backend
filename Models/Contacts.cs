namespace DeBillPay_Backend.Models;

public class Contact
{
    public int ContactId { get; set; }
    public string Status { get; set; } = null!;

    // FK
    public int UserId { get; set; }
    public User User { get; set; } = null!;

    public int FriendId { get; set; }
    public User Friend { get; set; } = null!;
}