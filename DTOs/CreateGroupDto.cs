namespace DeBillPay_Backend.DTOs
{
    public class CreateGroupDto
    {
        public string Name { get; set; } = null!;
        public List<int> FriendIds { get; set; } = new();
    }
}