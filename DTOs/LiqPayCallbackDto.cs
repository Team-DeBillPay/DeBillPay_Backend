namespace DeBillPay_Backend.DTOs;

public class LiqPayCallbackDto
{
    public string order_id { get; set; } = null!;
    public string status { get; set; } = null!;
    public decimal amount { get; set; }
    public string currency { get; set; } = null!;
    public string description { get; set; } = null!;
    public long transaction_id { get; set; }
}
