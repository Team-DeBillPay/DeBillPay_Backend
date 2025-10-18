namespace DeBillPay_Backend.Models;

public class Group
{
    public int GroupId { get; set; }
    public string Name { get; set; } = null!;

    public int UserId { get; set; }
    public User Owner { get; set; } = null!;

    public ICollection<GroupMember> Members { get; set; } = new List<GroupMember>();
}