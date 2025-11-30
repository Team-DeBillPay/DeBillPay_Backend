using MailKit.Net.Smtp;
using MimeKit;
using Microsoft.Extensions.Configuration;

namespace DeBillPay_Backend.Services;

public static class EmailService
{
	public static async Task SendEmailAsync(string toEmail, string subject, string body, IConfiguration config)
	{
		var emailMessage = new MimeMessage();
		emailMessage.From.Add(new MailboxAddress("DeBillPay", config["Email:From"]));
		emailMessage.To.Add(MailboxAddress.Parse(toEmail));
		emailMessage.Subject = subject;

		emailMessage.Body = new TextPart("plain")
		{
			Text = body
		};

		using var client = new SmtpClient();
		var smtpPortValue = config["Email:SmtpPort"];
		if (!int.TryParse(smtpPortValue, out int smtpPort))
			throw new InvalidOperationException("Email:SmtpPort is not configured or invalid.");

		await client.ConnectAsync(config["Email:SmtpHost"], smtpPort, MailKit.Security.SecureSocketOptions.StartTls);
		await client.AuthenticateAsync(config["Email:Username"], config["Email:Password"]);
		await client.SendAsync(emailMessage);
		await client.DisconnectAsync(true);
	}
}