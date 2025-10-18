namespace DeBillPay_Backend.Models;

public class Comment
{
    public int CommentId { get; set; }
    public string Text { get; set; } = null!;
    public DateTime CreatedAt { get; set; }

    public int EbillId { get; set; }
    public Ebill Ebill { get; set; } = null!;
}