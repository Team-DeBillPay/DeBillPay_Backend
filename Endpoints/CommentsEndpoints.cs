using Microsoft.AspNetCore.Cryptography.KeyDerivation;
using Microsoft.Extensions.Configuration;
using System.Security.Cryptography;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using DeBillPay_Backend.Data;
using DeBillPay_Backend.Models;
using Microsoft.EntityFrameworkCore;
using DeBillPay_Backend.DTOs;
using DeBillPay_Backend.Services;
using DeBillPay_Backend.Models.Validation;

namespace DeBillPay_Backend.Endpoints
{
    public static class CommentsEndpoints
    {
        public static void MapCommentsEndpoints(this WebApplication app)
        {
            app.MapPost("/api/ebills/{ebillId}/comments/create", async (
    CreateCommentDto dto,
    HttpContext http,
    ApplicationDbContext db
) =>
            {
                var userIdClaim = http.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (userIdClaim == null)
                    return Results.Unauthorized();

                int userId = int.Parse(userIdClaim);
                int ebillId = dto.EbillId;

                var ebill = await db.Ebills
                    .Include(e => e.Participants)
                    .FirstOrDefaultAsync(e => e.EbillId == ebillId);

                if (ebill == null)
                    return Results.NotFound("E-bill not found");

                bool isParticipantOrOwner =
                    ebill.OrganizerId == userId ||
                    ebill.Participants.Any(p => p.UserId == userId);

                if (!isParticipantOrOwner)
                    return Results.Json(new { error = "You do not have permission to comment this e-bill." }, statusCode: 403);

                if (string.IsNullOrWhiteSpace(dto.Text))
                    return Results.BadRequest("Comment text cannot be empty.");

                var comment = new Comment
                {
                    EbillId = ebillId,
                    UserId = userId,
                    Text = dto.Text,
                    CreatedAt = DateTime.UtcNow
                };

                db.Comments.Add(comment);
                await db.SaveChangesAsync();

                var user = await db.Users
                    .Where(u => u.UserId == userId)
                    .Select(u => new { u.UserId, u.FirstName, u.LastName })
                    .FirstAsync();

                return Results.Ok(new
                {
                    comment.CommentId,
                    comment.Text,
                    comment.CreatedAt,
                    User = user
                });
            })
.RequireAuthorization();
            app.MapGet("/api/ebills/{ebillId}/comments", async (
     int ebillId,
     HttpContext http,
     ApplicationDbContext db
 ) =>
            {
                var userIdClaim = http.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (userIdClaim == null)
                    return Results.Unauthorized();

                int userId = int.Parse(userIdClaim);

                var ebill = await db.Ebills
                    .Include(e => e.Participants)
                    .FirstOrDefaultAsync(e => e.EbillId == ebillId);

                if (ebill == null)
                    return Results.NotFound("E-bill not found.");

                bool hasAccess =
                    ebill.OrganizerId == userId ||
                    ebill.Participants.Any(p => p.UserId == userId);

                if (!hasAccess)
                    return Results.Json(new { error = "You do not have permission to view comments." }, statusCode: 403);

                var comments = await db.Comments
                    .Where(c => c.EbillId == ebillId)
                    .OrderBy(c => c.CreatedAt)
                    .Select(c => new
                    {
                        c.CommentId,
                        c.Text,
                        c.CreatedAt,
                        User = new
                        {
                            c.User.UserId,
                            c.User.FirstName,
                            c.User.LastName
                        }
                    })
                    .ToListAsync();

                return Results.Ok(comments);
            })
 .RequireAuthorization();

        }
    }
}

