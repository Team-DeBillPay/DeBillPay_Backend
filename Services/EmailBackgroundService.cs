using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Configuration;

namespace DeBillPay_Backend.Services
{
    public class EmailBackgroundService : BackgroundService
    {
        private readonly EmailQueue _queue;
        private readonly IConfiguration _config;

        public EmailBackgroundService(EmailQueue queue, IConfiguration config)
        {
            _queue = queue;
            _config = config;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            Console.WriteLine("[EmailWorker] Started");

            while (!stoppingToken.IsCancellationRequested)
            {
                var emailTask = _queue.Dequeue();

                if (emailTask != null)
                {
                    try
                    {
                        await EmailService.SendEmailAsync(
                            emailTask.To,
                            emailTask.Subject,
                            emailTask.Body,
                            _config
                        );

                        Console.WriteLine($"[EmailWorker] Sent email to {emailTask.To}");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[EmailWorker] Error sending email: {ex.Message}");
                    }
                }

                await Task.Delay(100, stoppingToken); // throttle
            }

            Console.WriteLine("[EmailWorker] Stopped");
        }
    }
}