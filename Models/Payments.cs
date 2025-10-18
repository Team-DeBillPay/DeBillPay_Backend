namespace DeBillPay_Backend.Models;

public class Payment
{
    public int PaymentId { get; set; }
    public DateTime TransactionDate { get; set; }
    public string Status { get; set; } = null!;
    public decimal Amount { get; set; }
    public string TransactionReference { get; set; } = null!;

    public int UserId { get; set; }
    public User User { get; set; } = null!;

    public int EbillId { get; set; }
    public Ebill Ebill { get; set; } = null!;
}