namespace DeBillPay_Backend.DTOs
{
    public class CreateCommentDto
    {
        public int EbillId { get; set; }
        public string Text { get; set; } = null!;
    }
}