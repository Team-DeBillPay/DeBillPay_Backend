namespace DeBillPay_Backend.DTOs
{
    public class NotificationItemDto
    {
        public int? Id { get; set; }
        public required string Type { get; set; }
        public required string Message { get; set; }
        public required string Status { get; set; }
        public DateTime CreatedAt { get; set; }

        public int? EbillId { get; set; }
        public int? SenderId { get; set; }
    }
}