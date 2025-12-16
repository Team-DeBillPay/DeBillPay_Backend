using Resend;

namespace DeBillPay_Backend.Services;

public class EmailService
{
    private readonly ResendClient _resend;

    public EmailService(ResendClient resend)
    {
        _resend = resend;
    }

    public async Task SendEmailAsync(string to, string subject, string body)
    {
        try
        {
            var email = new EmailMessage
            {
                From = "DeBillPay <onboarding@resend.dev>",
                To = to,
                Subject = subject,
                TextBody = body
            };

            await _resend.EmailSendAsync(email);

            Console.WriteLine($"[EmailService] Email sent to {to}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[EmailService] Failed to send email: {ex.Message}");
        }
    }
}