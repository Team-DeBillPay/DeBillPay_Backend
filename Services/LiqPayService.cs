using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using DeBillPay_Backend.Configuration;
using Microsoft.Extensions.Options;

namespace DeBillPay_Backend.Services;

public class LiqPayService
{
    private readonly LiqPayOptions _options;

    public LiqPayService(IOptions<LiqPayOptions> options)
    {
        _options = options.Value;
    }

    public (string Data, string Signature) CreatePaymentData(
        decimal amount,
        string currency,
        string description,
        string orderId)
    {
        var payload = new
        {
            public_key = _options.PublicKey,
            version = 3,
            action = "pay",
            amount = amount,
            currency = currency,
            description = description,
            order_id = orderId,
            sandbox = _options.Sandbox ? 1 : 0,
            server_url = _options.ServerUrl,
            result_url = _options.ResultUrl
        };

        var json = JsonSerializer.Serialize(payload);
        var dataBytes = Encoding.UTF8.GetBytes(json);
        var data = Convert.ToBase64String(dataBytes);

        var signString = _options.PrivateKey + data + _options.PrivateKey;

        using var sha1 = SHA1.Create();
        var hash = sha1.ComputeHash(Encoding.UTF8.GetBytes(signString));
        var signature = Convert.ToBase64String(hash);

        return (data, signature);
    }

    public bool VerifySignature(string data, string signatureFromLiqPay)
    {
        var signString = _options.PrivateKey + data + _options.PrivateKey;

        using var sha1 = SHA1.Create();
        var hash = sha1.ComputeHash(Encoding.UTF8.GetBytes(signString));
        var mySignature = Convert.ToBase64String(hash);

        return mySignature == signatureFromLiqPay;
    }
}
