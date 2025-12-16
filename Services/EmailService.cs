using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;
using Microsoft.Extensions.Configuration;

namespace DeBillPay_Backend.Services;

public class EmailService
{
    private readonly IConfiguration _config;

    public EmailService(IConfiguration config)
    {
        _config = config;
    }

    public async Task SendEmailAsync(string to, string subject, string body)
    {
        var message = new MimeMessage();
        message.From.Add(MailboxAddress.Parse(_config["Email:From"]));
        message.To.Add(MailboxAddress.Parse(to));
        message.Subject = subject;

        message.Body = new TextPart("plain")
        {
            Text = body
        };

        using var client = new SmtpClient();

        try
        {
            await client.ConnectAsync(
                _config["Email:SmtpHost"],
                int.Parse(_config["Email:SmtpPort"]),
                SecureSocketOptions.StartTls
            );

            await client.AuthenticateAsync(
                _config["Email:Username"],
                _config["Email:Password"]
            );

            await client.SendAsync(message);
            await client.DisconnectAsync(true);

            Console.WriteLine($"[EmailService] Sent email to {to}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[EmailService] Failed to send email to {to}: {ex.Message}");
            throw; // ⚠️ важливо
        }
    }
}