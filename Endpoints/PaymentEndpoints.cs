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


public class PaymentEndpointsLogger
{

}

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
            var logger = httpContext.RequestServices.GetRequiredService<ILogger<PaymentEndpointsLogger>>();

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

            var amount = request.Amount ?? participant.Balance;
            if (amount <= 0)
                return Results.BadRequest("Amount must be greater than zero.");

            if (amount > participant.Balance)
                return Results.BadRequest($"Amount ({amount}) exceeds your remaining balance ({participant.Balance}).");

            var orderId = $"ebill-{ebill.EbillId}-user-{userId}-{Guid.NewGuid():N}";

            var resultUrl = $"http://localhost:5141/checks/{ebill.EbillId}";

            logger.LogInformation("Creating payment: OrderId={OrderId}, Amount={Amount}, UserId={UserId}",
                orderId, amount, userId);

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
                TransactionDate = DateTime.UtcNow.AddHours(2),
                TransactionReference = orderId
            };

            db.Payments.Add(payment);
            await db.SaveChangesAsync();

            logger.LogInformation("Payment created in DB: PaymentId={PaymentId}", payment.PaymentId);

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
            var logger = serviceProvider.GetRequiredService<ILogger<PaymentEndpointsLogger>>();

            logger.LogInformation("=== LIQPAY CALLBACK STARTED ===");

            try
            {
                if (!request.HasFormContentType)
                {
                    logger.LogError("Callback: Expected form content");
                    return Results.BadRequest("Expected form content.");
                }

                var form = await request.ReadFormAsync();
                var data = form["data"].ToString();
                var signature = form["signature"].ToString();

                logger.LogInformation("Callback received: Data length={DataLength}", data?.Length);

                if (string.IsNullOrWhiteSpace(data) || string.IsNullOrWhiteSpace(signature))
                {
                    logger.LogError("Callback: Missing data or signature");
                    return Results.BadRequest("Missing data or signature.");
                }

                if (!liqPay.VerifySignature(data, signature))
                {
                    logger.LogError("Callback: Signature verification failed");
                    return Results.Unauthorized();
                }

                logger.LogInformation("Callback: Signature verified successfully");

                string json;
                try
                {
                    json = Encoding.UTF8.GetString(Convert.FromBase64String(data));
                    logger.LogInformation("Callback JSON: {Json}", json);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Callback: Invalid base64 data");
                    return Results.BadRequest("Invalid base64 data.");
                }

                var callback = JsonSerializer.Deserialize<LiqPayCallbackDto>(json, JsonOptions);
                if (callback is null || string.IsNullOrEmpty(callback.order_id))
                {
                    logger.LogError("Callback: Invalid callback data");
                    return Results.BadRequest("Invalid callback data.");
                }

                logger.LogInformation("Callback: OrderId={OrderId}, Status={Status}, Amount={Amount}",
                    callback.order_id, callback.status, callback.amount);

                var payment = await db.Payments
                    .Include(p => p.Ebill)
                    .FirstOrDefaultAsync(p => p.TransactionReference == callback.order_id);

                if (payment is null)
                {
                    logger.LogError("Callback: Payment not found for OrderId={OrderId}", callback.order_id);
                    return Results.NotFound("Payment not found.");
                }

                logger.LogInformation("Callback: Found payment ID={PaymentId}, Current status={CurrentStatus}",
                    payment.PaymentId, payment.Status);

                await using var tx = await db.Database.BeginTransactionAsync();

                try
                {
                    var oldStatus = payment.Status;

                    payment.TransactionDate = DateTime.UtcNow.AddHours(2);
                    payment.Status = callback.status;

                    logger.LogInformation("Callback: Updated payment status from {OldStatus} to {NewStatus}",
                        oldStatus, callback.status);

                    if ((callback.status.ToLower() == "success" || callback.status.ToLower() == "sandbox") && oldStatus != "success")
                    {
                        var participant = await db.EbillParticipants
                            .Include(p => p.User)
                            .FirstOrDefaultAsync(p => p.EbillId == payment.EbillId && p.UserId == payment.UserId);

                        if (participant is null)
                        {
                            logger.LogError("Callback: Participant not found for EbillId={EbillId}, UserId={UserId}",
                                payment.EbillId, payment.UserId);
                            await tx.RollbackAsync();
                            return Results.NotFound("Participant record not found.");
                        }

                        logger.LogInformation("Callback: Found participant - Assigned={Assigned}, Paid={Paid}, Balance={Balance}, PaymentStatus={PaymentStatus}",
                            participant.AssignedAmount, participant.PaidAmount, participant.Balance, participant.PaymentStatus);

                        var newPaidAmount = Decimal.Round(participant.PaidAmount + payment.Amount, 2, MidpointRounding.AwayFromZero);
                        var newBalance = Decimal.Round(participant.AssignedAmount - newPaidAmount, 2, MidpointRounding.AwayFromZero);

                        participant.PaidAmount = newPaidAmount;
                        participant.Balance = newBalance;

                        logger.LogInformation("Callback: Updated participant - New Paid={NewPaid}, New Balance={NewBalance}",
                            participant.PaidAmount, participant.Balance);

                        if (participant.Balance <= 0)
                        {
                            participant.Balance = 0;
                            participant.PaymentStatus = "погашений";
                            logger.LogInformation("Callback: Participant fully paid (погашений)");
                        }
                        else if (participant.PaidAmount > 0)
                        {
                            participant.PaymentStatus = "частково погашений";
                            logger.LogInformation("Callback: Participant partially paid (частково погашений)");
                        }
                        else
                        {
                            participant.PaymentStatus = "непогашений";
                            logger.LogInformation("Callback: Participant not paid (непогашений)");
                        }

                        string historyAction = participant.Balance <= 0 ? "full_payment" : "partial_payment";
                        string historyMessage = participant.Balance <= 0
                            ? $"{participant.User.FirstName} повністю погасив(-ла) свій борг"
                            : $"{participant.User.FirstName} частково погасив(-ла) свій борг";

                        await EbillHistoryService.AddAsync(db, payment.EbillId, participant.UserId, historyAction, historyMessage);

                        if (!string.IsNullOrWhiteSpace(participant.User.Email))
                        {
                            try
                            {
                                var config = serviceProvider.GetRequiredService<IConfiguration>();
                                string emailSubject = "Статус вашого платежу DeBillPay";
                                string emailBody = participant.Balance <= 0
                                    ? $"Привіт {participant.User.FirstName},\n\nВи повністю погасили свій борг по чеку \"{payment.Ebill.Name}\". Дякуємо за своєчасну оплату!"
                                    : $"Привіт {participant.User.FirstName},\n\nВи частково погасили свій борг по чеку \"{payment.Ebill.Name}\". Залишок до оплати: {participant.Balance} {payment.Ebill.Currency}.";

                                await EmailService.SendEmailAsync(
                                    participant.User.Email,
                                    emailSubject,
                                    emailBody,
                                    config
                                );
                                logger.LogInformation("Callback: Email sent to {Email}", participant.User.Email);
                            }
                            catch (Exception emailEx)
                            {
                                logger.LogError(emailEx, "Callback: Failed to send email");
                            }
                        }

                        var allParticipants = await db.EbillParticipants
                            .Where(p => p.EbillId == payment.EbillId)
                            .ToListAsync();

                        bool allPaid = allParticipants.All(p => p.PaymentStatus == "погашений" || p.Balance <= 0);
                        bool anyPartial = allParticipants.Any(p => p.PaymentStatus == "частково погашений");

                        if (allPaid)
                        {
                            payment.Ebill.Status = "закритий";
                        }
                        else if (anyPartial)
                        {
                            payment.Ebill.Status = "активний";
                        }

                        payment.Ebill.UpdatedAt = DateTime.UtcNow.AddHours(2);

                        logger.LogInformation("Callback: Ebill status updated to {EbillStatus}", payment.Ebill.Status);
                    }

                    await db.SaveChangesAsync();
                    await tx.CommitAsync();

                    logger.LogInformation("=== LIQPAY CALLBACK COMPLETED SUCCESSFULLY ===");
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Callback: Transaction failed");
                    await tx.RollbackAsync();
                    throw;
                }

                return Results.Text("ok", "text/plain", Encoding.UTF8);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Callback: Unhandled exception");
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
            var logger = httpContext.RequestServices.GetRequiredService<ILogger<PaymentEndpointsLogger>>();

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

            logger.LogInformation("Status check: OrderId={OrderId}, Status={Status}", orderId, payment.Status);

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