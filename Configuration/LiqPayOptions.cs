namespace DeBillPay_Backend.Configuration;

public class LiqPayOptions
{
    public string PublicKey { get; set; } = null!;
    public string PrivateKey { get; set; } = null!;
    public bool Sandbox { get; set; }
    public string ServerUrl { get; set; } = null!;
    public string ResultUrl { get; set; } = null!;
}
