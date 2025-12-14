using System;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using DeBillPay_Backend.Data;
using DeBillPay_Backend.DTOs;
using DeBillPay_Backend.Models;
using DeBillPay_Backend.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;

namespace DeBillPay_Backend.Endpoints;

public static class PaymentEndpoints
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public static void MapPaymentEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/payments");

        group.MapPost("/create", [Authorize] async (
            ApplicationDbContext db,
            LiqPayService liqPay,
            HttpContext httpContext,
            CreatePaymentRequestDto request) =>
        {
            var userIdClaim = httpContext.User.FindFirst(ClaimTypes.NameIdentifier) ??
                              httpContext.User.FindFirst("sub");

            if (userIdClaim is null)
                return Results.Unauthorized();

            if (request.EbillId <= 0)
                return Results.BadRequest("Invalid EbillId.");

            var userId = int.Parse(userIdClaim.Value);

            var ebill = await db.Ebills
                .FirstOrDefaultAsync(e => e.EbillId == request.EbillId);

            if (ebill is null)
                return Results.NotFound("Ebill not found");

            var participant = await db.EbillParticipants
                .FirstOrDefaultAsync(p => p.EbillId == request.EbillId && p.UserId == userId);

            if (participant is null)
                return Results.NotFound("Participant not found for this user and ebill.");

            var remainingToPay = participant.AssignedAmount - participant.Balance;

            var amount = request.Amount ?? remainingToPay;
            if (amount <= 0)
                return Results.BadRequest("Amount must be greater than zero.");

            if (amount > remainingToPay)
                return Results.BadRequest($"Amount ({amount}) exceeds your remaining balance ({remainingToPay}).");

            var orderId = $"ebill-{ebill.EbillId}-user-{userId}-{Guid.NewGuid():N}";

            var resultUrl = $"http://localhost:5173/checks/{ebill.EbillId}";

            var (data, signature) = liqPay.CreatePaymentData(
                amount: amount,
                currency: ebill.Currency,
                description: ebill.Name,
                orderId: orderId,
                resultUrl: resultUrl
            );

            var payment = new Payment
            {
                EbillId = ebill.EbillId,
                UserId = userId,
                Amount = amount,
                Status = "pending",
                TransactionDate = DateTime.UtcNow,
                TransactionReference = orderId
            };

            db.Payments.Add(payment);
            await db.SaveChangesAsync();

            return Results.Ok(new
            {
                data,
                signature,
                paymentId = payment.PaymentId,
                orderId = orderId
            });
        });

        group.MapPost("/callback", async (
            ApplicationDbContext db,
            LiqPayService liqPay,
            HttpRequest request,
            IServiceProvider serviceProvider) =>
        {
            try
            {
                if (!request.HasFormContentType)
                    return Results.BadRequest("Expected form content.");

                var form = await request.ReadFormAsync();
                var data = form["data"].ToString();
                var signature = form["signature"].ToString();

                if (string.IsNullOrWhiteSpace(data) || string.IsNullOrWhiteSpace(signature))
                    return Results.BadRequest("Missing data or signature.");

                if (!liqPay.VerifySignature(data, signature))
                    return Results.Unauthorized();

                string json;
                try
                {
                    json = Encoding.UTF8.GetString(Convert.FromBase64String(data));
                }
                catch
                {
                    return Results.BadRequest("Invalid base64 data.");
                }

                var callback = JsonSerializer.Deserialize<LiqPayCallbackDto>(json, JsonOptions);
                if (callback is null || string.IsNullOrEmpty(callback.order_id))
                    return Results.BadRequest("Invalid callback data.");

                var payment = await db.Payments
                    .Include(p => p.Ebill)
                    .FirstOrDefaultAsync(p => p.TransactionReference == callback.order_id);

                if (payment is null)
                    return Results.NotFound("Payment not found.");

                await using var tx = await db.Database.BeginTransactionAsync();

                try
                {
                    var oldStatus = payment.Status;

                    payment.TransactionDate = DateTime.UtcNow;
                    payment.Status = callback.status;

                    if ((callback.status.ToLower() == "success" || callback.status.ToLower() == "sandbox") && oldStatus != "success")
                    {
                        var participant = await db.EbillParticipants
                            .Include(p => p.User)
                            .FirstOrDefaultAsync(p => p.EbillId == payment.EbillId && p.UserId == payment.UserId);

                        if (participant is null)
                        {
                            await tx.RollbackAsync();
                            return Results.NotFound("Participant record not found.");
                        }

                        participant.Balance = Decimal.Round(participant.Balance + payment.Amount, 2, MidpointRounding.AwayFromZero);
                        participant.PaidAmount = participant.Balance;

                        if (participant.Balance >= participant.AssignedAmount)
                        {
                            participant.Balance = participant.AssignedAmount;
                            participant.PaymentStatus = "погашений";
                        }
                        else if (participant.Balance > 0)
                        {
                            participant.PaymentStatus = "частково погашений";
                        }
                        else
                        {
                            participant.PaymentStatus = "непогашений";
                        }

                        string historyAction = participant.Balance >= participant.AssignedAmount ? "full_payment" : "partial_payment";
                        string historyMessage = participant.Balance >= participant.AssignedAmount
                            ? $"{participant.User.FirstName} повністю погасив(-ла) свій борг"
                            : $"{participant.User.FirstName} частково погасив(-ла) свій борг";

                      
                        if (!string.IsNullOrWhiteSpace(participant.User.Email))
                        {
                            var emailQueue = serviceProvider.GetRequiredService<EmailQueue>();

                            string emailSubject = "Статус вашого платежу DeBillPay";
                            string emailBody = participant.Balance >= participant.AssignedAmount
                                ? $"Привіт {participant.User.FirstName},\n\nВи повністю погасили свій борг по чеку \"{payment.Ebill.Name}\". Дякуємо за своєчасну оплату!"
                                : $"Привіт {participant.User.FirstName},\n\nВи частково погасили свій борг по чеку \"{payment.Ebill.Name}\". Залишок до оплати: {participant.AssignedAmount - participant.Balance} {payment.Ebill.Currency}.";

                            emailQueue.Enqueue(new EmailTask
                            {
                                To = participant.User.Email,
                                Subject = emailSubject,
                                Body = emailBody
                            });
                        }

                        var allParticipants = await db.EbillParticipants
                            .Where(p => p.EbillId == payment.EbillId)
                            .ToListAsync();

                        bool allPaid = allParticipants.All(p => p.Balance >= p.AssignedAmount);
                        bool anyPartial = allParticipants.Any(p => p.Balance > 0 && p.Balance < p.AssignedAmount);

                        if (allPaid)
                        {
                            payment.Ebill.Status = "закритий";
                        }
                        else if (anyPartial)
                        {
                            payment.Ebill.Status = "активний";
                        }

                        payment.Ebill.UpdatedAt = DateTime.UtcNow;
                    }

                    await db.SaveChangesAsync();
                    await tx.CommitAsync();

                    return Results.Text("ok", "text/plain", Encoding.UTF8);
                }
                catch (Exception)
                {
                    await tx.RollbackAsync();
                    throw;
                }
            }
            catch (Exception)
            {
                return Results.Problem("Internal server error", statusCode: 500);
            }
        })
                .AllowAnonymous()
        .DisableAntiforgery();

        group.MapGet("/status/{orderId}", [Authorize] async (
            string orderId,
            ApplicationDbContext db,
            HttpContext httpContext) =>
        {
            var userIdClaim = httpContext.User.FindFirst(ClaimTypes.NameIdentifier) ??
                              httpContext.User.FindFirst("sub");

            if (userIdClaim is null)
                return Results.Unauthorized();

            var userId = int.Parse(userIdClaim.Value);

            var payment = await db.Payments
                .Include(p => p.Ebill)
                .FirstOrDefaultAsync(p => p.TransactionReference == orderId && p.UserId == userId);

            if (payment == null)
                return Results.NotFound();

            return Results.Ok(new
            {
                payment.Status,
                payment.Amount,
                payment.TransactionDate,
                EbillId = payment.EbillId,
                EbillStatus = payment.Ebill?.Status
            });
        });
    }
}