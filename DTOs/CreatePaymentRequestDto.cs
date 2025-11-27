namespace DeBillPay_Backend.DTOs;

public class CreatePaymentRequestDto
{
    public int EbillId { get; set; }
    public decimal? Amount { get; set; }
}
