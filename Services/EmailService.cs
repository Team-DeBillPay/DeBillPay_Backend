using MailKit.Net.Smtp;
using MimeKit;
using Microsoft.Extensions.Configuration;

namespace DeBillPay_Backend.Services;

public static class EmailService
{
    public static async Task SendEmailAsync(string toEmail, string subject, string body, IConfiguration config)
    {
        // Перевірка валідності формату email
        if (!MailboxAddress.TryParse(toEmail, out var parsedAddress))
        {
            Console.WriteLine($"[EmailService] Invalid email format: {toEmail}");
            return; // просто пропускаємо відправку
        }

        try
        {
            var emailMessage = new MimeMessage();
            emailMessage.From.Add(new MailboxAddress("DeBillPay", config["Email:From"]));
            emailMessage.To.Add(parsedAddress);
            emailMessage.Subject = subject;

            emailMessage.Body = new TextPart("plain") { Text = body };

            using var client = new SmtpClient();

            if (!int.TryParse(config["Email:SmtpPort"], out int smtpPort))
                throw new InvalidOperationException("Email:SmtpPort invalid or missing");

            await client.ConnectAsync(
                config["Email:SmtpHost"],
                smtpPort,
                MailKit.Security.SecureSocketOptions.StartTls
            );

            await client.AuthenticateAsync(config["Email:Username"], config["Email:Password"]);
            await client.SendAsync(emailMessage);
            await client.DisconnectAsync(true);
        }
        catch (Exception ex)
        {
            // Лог, але API не падає
            Console.WriteLine($"[EmailService] Failed to send email to {toEmail}: {ex.Message}");
        }
    }
}