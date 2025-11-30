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

            var amount = request.Amount ?? participant.Balance;
            if (amount <= 0)
                return Results.BadRequest("Amount must be greater than zero.");

            if (amount > participant.Balance)
                return Results.BadRequest("Amount exceeds your remaining balance.");

            var orderId = $"ebill-{ebill.EbillId}-user-{userId}-{Guid.NewGuid():N}";

            var (data, signature) = liqPay.CreatePaymentData(
                amount: amount,
                currency: ebill.Currency,
                description: ebill.Name,
                orderId: orderId
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
                signature
            });
        });

        group.MapPost("/callback", async (
            ApplicationDbContext db,
            LiqPayService liqPay,
            HttpRequest request) =>
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

            var callback = JsonSerializer.Deserialize<LiqPayCallbackDto>(json);
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
                var alreadySuccess = payment.Status == "success";

                payment.TransactionDate = DateTime.UtcNow;
                payment.Status = callback.status;

                if (callback.status == "success" && !alreadySuccess)
                {
                    var participant = await db.EbillParticipants
                        .FirstOrDefaultAsync(p => p.EbillId == payment.EbillId && p.UserId == payment.UserId);

                    if (participant is null)
                    {
                        await tx.RollbackAsync();
                        return Results.NotFound("Participant record not found.");
                    }

                    participant.PaidAmount = Decimal.Round(participant.PaidAmount + payment.Amount, 2, MidpointRounding.AwayFromZero);
                    participant.Balance = Decimal.Round(participant.AssignedAmount - participant.PaidAmount, 2, MidpointRounding.AwayFromZero);

                    if (participant.Balance <= 0)
                    {
                        participant.Balance = 0;
                        participant.PaymentStatus = "paid";
                    }
                    else
                    {
                        participant.PaymentStatus = "partial";
                    }

                    var anyRemaining = await db.EbillParticipants
                        .AnyAsync(p => p.EbillId == payment.EbillId && p.Balance > 0);

                    payment.Ebill.Status = anyRemaining ? "partial" : "paid";
                }

                await db.SaveChangesAsync();
                await tx.CommitAsync();
            }
            catch
            {
                await tx.RollbackAsync();
                throw;
            }

            return Results.Ok();
        })
        .AllowAnonymous();
    }
}
