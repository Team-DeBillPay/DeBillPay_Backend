namespace DeBillPay_Backend.Models;

public class GroupMember
{
    public int GroupMemberId { get; set; }

    // FK
    public int MemberId { get; set; }
    public User Member { get; set; } = null!;

    public int GroupId { get; set; }
    public Group Group { get; set; } = null!;
}