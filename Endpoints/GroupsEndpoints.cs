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
    public static class GroupsEndpoints
    {
        public static void MapGroupsEndpoints(this WebApplication app)
        {


            app.MapPost("/api/groups/create", async (
    CreateGroupDto dto,
    HttpContext http,
    ApplicationDbContext db
) =>
            {
                var userId = http.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (userId is null)
                    return Results.Json(new { error = "Unauthorized" }, statusCode: 401);

                int userIdInt = int.Parse(userId);

                if (string.IsNullOrWhiteSpace(dto.Name))
                    return Results.BadRequest(new { error = "Group name is required" });

                var validFriends = await db.Contacts
                    .Where(c => c.UserId == userIdInt &&
                                dto.FriendIds.Contains(c.FriendId) &&
                                c.Status == "active")
                    .Select(c => c.FriendId)
                    .ToListAsync();

                var invalidIds = dto.FriendIds.Except(validFriends).ToList();
                if (invalidIds.Count > 0)
                {
                    return Results.BadRequest(new
                    {
                        error = "Some friends not found",
                        invalidFriendIds = invalidIds
                    });
                }

                if (validFriends.Count == 0)
                {
                    return Results.BadRequest(new
                    {
                        error = "You cannot create a group without members"
                    });
                }

                var group = new Group
                {
                    Name = dto.Name,
                    UserId = userIdInt, 
                };

                db.Groups.Add(group);
                await db.SaveChangesAsync();

                db.GroupMembers.Add(new GroupMember
                {
                    GroupId = group.GroupId,
                    MemberId = userIdInt
                });

                foreach (var friendId in validFriends)
                {
                    db.GroupMembers.Add(new GroupMember
                    {
                        GroupId = group.GroupId,
                        MemberId = friendId
                    });
                }

                await db.SaveChangesAsync();

                return Results.Ok(new
                {
                    message = "Group created successfully",
                    groupId = group.GroupId,
                    members = validFriends.Prepend(userIdInt)
                });
            })
.RequireAuthorization();

            app.MapGet("/api/groups", async (HttpContext http, ApplicationDbContext db) =>
            {
                var userIdClaim = http.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (userIdClaim is null)
                    return Results.Json(new { error = "Unauthorized" }, statusCode: 401);

                int userId = int.Parse(userIdClaim);

                var groups = await db.Groups
                    .Where(g => g.UserId == userId)
                    .Include(g => g.Members)
                        .ThenInclude(m => m.Member)
                    .Select(g => new
                    {
                        g.GroupId,
                        g.Name,
                        Members = g.Members.Select(m => new
                        {
                            m.MemberId,
                            m.Member.UserId,
                            m.Member.FirstName,
                            m.Member.LastName,
                            m.Member.Email,
                            m.Member.PhoneNumber
                        })
                    })
                    .ToListAsync();

                return Results.Ok(groups);
            })
.RequireAuthorization();
            app.MapGet("/api/groups/{groupId:int}", async (
    int groupId,
    HttpContext http,
    ApplicationDbContext db
) =>
{
    var userIdClaim = http.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
    if (userIdClaim is null)
        return Results.Json(new { error = "Unauthorized" }, statusCode: 401);

    int userId = int.Parse(userIdClaim);

    var group = await db.Groups
        .Where(g => g.GroupId == groupId && g.UserId == userId)
        .Include(g => g.Members)
            .ThenInclude(m => m.Member)
        .Select(g => new
        {
            g.GroupId,
            g.Name,
            Members = g.Members.Select(m => new
            {
                m.MemberId,
                m.Member.UserId,
                m.Member.FirstName,
                m.Member.LastName,
                m.Member.Email,
                m.Member.PhoneNumber
            })
        })
        .FirstOrDefaultAsync();

    if (group == null)
        return Results.NotFound(new { error = "Group not found" });

    return Results.Ok(group);
})
.RequireAuthorization();
            app.MapDelete("/api/groups/{groupId:int}/delete", async (
    int groupId,
    HttpContext http,
    ApplicationDbContext db
) =>
            {
                var userIdClaim = http.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (userIdClaim is null)
                    return Results.Json(new { error = "Unauthorized" }, statusCode: 401);

                int userId = int.Parse(userIdClaim);

                var group = await db.Groups
                    .Include(g => g.Members)
                    .FirstOrDefaultAsync(g => g.GroupId == groupId && g.UserId == userId);

                if (group == null)
                    return Results.NotFound(new { error = "Group not found or access denied" });

                db.GroupMembers.RemoveRange(group.Members);

                db.Groups.Remove(group);

                await db.SaveChangesAsync();

                return Results.Ok(new { message = "Group deleted successfully" });
            })
.RequireAuthorization();
        }
    }
}

