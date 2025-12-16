using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Configuration;

namespace DeBillPay_Backend.Services
{
    public class EmailBackgroundService : BackgroundService
    {
        private readonly EmailQueue _queue;
        private readonly IServiceScopeFactory _scopeFactory;

        public EmailBackgroundService(
            EmailQueue queue,
            IServiceScopeFactory scopeFactory)
        {
            _queue = queue;
            _scopeFactory = scopeFactory;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            Console.WriteLine("[EmailWorker] Started");

            while (!stoppingToken.IsCancellationRequested)
            {
                var task = _queue.Dequeue();

                if (task != null)
                {
                    using var scope = _scopeFactory.CreateScope();
                    var emailService = scope.ServiceProvider.GetRequiredService<EmailService>();

                    try
                    {
                        await emailService.SendEmailAsync(
                            task.To,
                            task.Subject,
                            task.Body
                        );
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[EmailWorker] Email failed: {ex.Message}");
                    }
                }

                await Task.Delay(100, stoppingToken);
            }
        }
    }
}