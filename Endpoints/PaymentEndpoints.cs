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

            var userId = int.Parse(userIdClaim.Value);

            var ebill = await db.Ebills
                .FirstOrDefaultAsync(e => e.EbillId == request.EbillId);

            if (ebill is null)
                return Results.NotFound("Ebill not found");

            var amount = request.Amount ?? ebill.AmountOfDept;
            if (amount <= 0)
                return Results.BadRequest("Amount must be greater than zero.");

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

            var json = Encoding.UTF8.GetString(Convert.FromBase64String(data));
            var callback = JsonSerializer.Deserialize<LiqPayCallbackDto>(json);

            if (callback is null || string.IsNullOrEmpty(callback.order_id))
                return Results.BadRequest("Invalid callback data.");

            var payment = await db.Payments
                .FirstOrDefaultAsync(p => p.TransactionReference == callback.order_id);

            if (payment is null)
                return Results.NotFound("Payment not found.");

            payment.Status = callback.status;
            payment.TransactionDate = DateTime.UtcNow;

            await db.SaveChangesAsync();

            return Results.Ok();
        })
        .AllowAnonymous();
    }
}
