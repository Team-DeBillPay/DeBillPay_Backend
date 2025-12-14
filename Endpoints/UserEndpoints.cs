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
    public static class UserEndpoints
    {
        public static void MapUserEndpoints(this WebApplication app)
        {

            app.MapPost("/api/auth/register", async (ApplicationDbContext db, RegisterDto dto) =>
            {
                if (await db.Users.AnyAsync(u => u.Email == dto.Email))
                    return Results.BadRequest("User already exists");

                var normalizedNewPhone = UkrainianPhoneAttribute.NormalizePhone(dto.PhoneNumber);
                var existingUsers = await db.Users.ToListAsync();
                if (existingUsers.Any(u => UkrainianPhoneAttribute.NormalizePhone(u.PhoneNumber) == normalizedNewPhone))
                    return Results.BadRequest("Phone number already exists");

                if (string.IsNullOrEmpty(dto.Password) || dto.Password.Length < 6)
                    return Results.BadRequest("Password must be at least 6 characters");

                var phoneAttr = new UkrainianPhoneAttribute();
                if (!phoneAttr.IsValid(dto.PhoneNumber))
                    return Results.BadRequest("Invalid phone number format");

                var user = new User
                {
                    FirstName = dto.FirstName,
                    LastName = dto.LastName,
                    Email = dto.Email,
                    PhoneNumber = dto.PhoneNumber,
                    PasswordHash = PasswordHasher.HashPassword(dto.Password)
                };

                db.Users.Add(user);
                await db.SaveChangesAsync();

                await NotificationService.CreateAsync(
                    db,
                    user.UserId,
                    "welcome",
                    $"Вітаємо, {user.FirstName}! Ваш акаунт успішно створено."
                );

                if (!string.IsNullOrWhiteSpace(user.Email))
                {
                    var queue = app.Services.GetRequiredService<EmailQueue>();

                    queue.Enqueue(new EmailTask
                    {
                        To = user.Email,
                        Subject = "Ласкаво просимо до DeBillPay",
                        Body = $"Привіт {user.FirstName},\n\nВаш акаунт успішно створено. Тепер ви можете користуватися нашим сервісом."
                    });
                }
                return Results.Ok("User registered successfully");
            });

            app.MapPost("/api/auth/login", async (ApplicationDbContext db, IConfiguration config, LoginDto dto) =>
            {
                User? user = null;

                if (!string.IsNullOrEmpty(dto.Email))
                {
                    user = await db.Users.FirstOrDefaultAsync(u => u.Email == dto.Email);
                }
                else if (!string.IsNullOrEmpty(dto.PhoneNumber))
                {
                    var normalizedPhone = UkrainianPhoneAttribute.NormalizePhone(dto.PhoneNumber);

                    user = db.Users
                        .AsEnumerable() 
                        .FirstOrDefault(u =>
                            UkrainianPhoneAttribute.NormalizePhone(u.PhoneNumber) == normalizedPhone);
                }
                else
                {
                    return Results.BadRequest("Email or phone number is required");
                }

                if (user == null || !PasswordHasher.VerifyPassword(dto.Password, user.PasswordHash))
                    return Results.BadRequest("Invalid credentials");

                var jwtSettings = config.GetSection("Jwt");
                var keyString = jwtSettings["Key"];
                if (string.IsNullOrEmpty(keyString))
                    throw new Exception("JWT Key is missing in configuration");

                var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(keyString));
                var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

                var claims = new[]
                {
        new Claim(ClaimTypes.NameIdentifier, user.UserId.ToString()),
        new Claim(ClaimTypes.Email, user.Email),
        new Claim(ClaimTypes.GivenName, user.FirstName),
        new Claim(ClaimTypes.Surname, user.LastName)
    };

                var token = new JwtSecurityToken(
                    issuer: jwtSettings["Issuer"],
                    audience: jwtSettings["Audience"],
                    claims: claims,
                    expires: DateTime.UtcNow.AddMinutes(Convert.ToDouble(jwtSettings["ExpireMinutes"])),
                    signingCredentials: creds
                );

                var tokenString = new JwtSecurityTokenHandler().WriteToken(token);

                return Results.Ok(new
                {
                    token = tokenString,
                    user = new
                    {
                        id = user.UserId,
                        firstName = user.FirstName,
                        lastName = user.LastName,
                        email = user.Email,
                        phoneNumber = user.PhoneNumber
                    }
                });
            });

            app.MapPatch("/api/users/{id}", async (int id, HttpContext context, ApplicationDbContext db, UpdateUserDto dto) =>
            {
                var userIdClaim = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (userIdClaim == null || !int.TryParse(userIdClaim, out var userId) || userId != id)
                    return Results.Unauthorized();

                var user = await db.Users.FindAsync(id);
                if (user == null)
                    return Results.NotFound("User not found");

                if (!string.IsNullOrEmpty(dto.FirstName))
                {
                    if (dto.FirstName.Length < 1 || dto.FirstName.Length > 50)
                        return Results.BadRequest("FirstName must be between 1 and 50 characters");
                    user.FirstName = dto.FirstName;
                }

                if (!string.IsNullOrEmpty(dto.LastName))
                {
                    if (dto.LastName.Length < 1 || dto.LastName.Length > 50)
                        return Results.BadRequest("LastName must be between 1 and 50 characters");
                    user.LastName = dto.LastName;
                }

                var originalEmail = user.Email;

                if (!string.IsNullOrEmpty(dto.Email))
                {
                    if (!new System.ComponentModel.DataAnnotations.EmailAddressAttribute().IsValid(dto.Email))
                        return Results.BadRequest("Invalid email format");

                    if (dto.Email != user.Email && await db.Users.AnyAsync(u => u.Email == dto.Email))
                        return Results.BadRequest("Email already exists");

                    user.Email = dto.Email;
                }

                if (!string.IsNullOrEmpty(dto.PhoneNumber))
                {
                    var phoneAttr = new UkrainianPhoneAttribute();
                    if (!phoneAttr.IsValid(dto.PhoneNumber))
                        return Results.BadRequest("Invalid phone number format");
                    var normalizedNewPhone = UkrainianPhoneAttribute.NormalizePhone(dto.PhoneNumber);
                    var normalizedCurrentPhone = UkrainianPhoneAttribute.NormalizePhone(user.PhoneNumber);
                    if (normalizedNewPhone != normalizedCurrentPhone)
                    {
                        var existingUsers = await db.Users.ToListAsync();
                        if (existingUsers.Any(u => UkrainianPhoneAttribute.NormalizePhone(u.PhoneNumber) == normalizedNewPhone))
                            return Results.BadRequest("Phone number already exists");
                    }
                    user.PhoneNumber = dto.PhoneNumber;
                }

                if (!string.IsNullOrEmpty(dto.Password))
                {
                    if (dto.Password.Length < 6)
                        return Results.BadRequest("Password must be at least 6 characters");

                    user.PasswordHash = PasswordHasher.HashPassword(dto.Password);
                }

                await db.SaveChangesAsync();

                string? newToken = null;
                if (dto.Email != null && dto.Email != originalEmail)
                {
                    var jwtSettings = context.RequestServices.GetRequiredService<IConfiguration>().GetSection("Jwt");
                    var keyString = jwtSettings["Key"];
                    if (string.IsNullOrEmpty(keyString))
                        throw new Exception("JWT Key is missing in configuration");

                    var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(keyString));
                    var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

                    var claims = new[]
                    {
                        new Claim(ClaimTypes.NameIdentifier, user.UserId.ToString()),
                        new Claim(ClaimTypes.Email, user.Email),
                        new Claim(ClaimTypes.GivenName, user.FirstName),
                        new Claim(ClaimTypes.Surname, user.LastName)
                    };

                    var token = new JwtSecurityToken(
                        issuer: jwtSettings["Issuer"],
                        audience: jwtSettings["Audience"],
                        claims: claims,
                        expires: DateTime.UtcNow.AddMinutes(Convert.ToDouble(jwtSettings["ExpireMinutes"])),
                        signingCredentials: creds
                    );

                    newToken = new JwtSecurityTokenHandler().WriteToken(token);
                }

                return Results.Ok(new
                {
                    userId = user.UserId,
                    firstName = user.FirstName,
                    lastName = user.LastName,
                    email = user.Email,
                    phoneNumber = user.PhoneNumber,
                    token = newToken
                });
            }).RequireAuthorization();

            app.MapGet("/api/users", async (ApplicationDbContext db) =>
            {
                var users = await db.Users
                    .Select(u => new {
                        u.UserId,
                        u.FirstName,
                        u.LastName,
                        u.Email,
                        u.PhoneNumber
                    })
                    .ToListAsync();
                return Results.Ok(users);
            }).RequireAuthorization();

            app.MapGet("/api/users/{id}", async (int id, ApplicationDbContext db) =>
            {
                var user = await db.Users
                    .Where(u => u.UserId == id)
                    .Select(u => new {
                        u.UserId,
                        u.FirstName,
                        u.LastName,
                        u.Email,
                        u.PhoneNumber
                    })
                    .FirstOrDefaultAsync();

                return user is not null ? Results.Ok(user) : Results.NotFound();
            }).RequireAuthorization();

            app.MapPost("/api/auth/google", async (
                ApplicationDbContext db,
                IConfiguration config,
                IGoogleAuthService googleAuthService,
                GoogleAuthDto dto) =>
                        {
                var googleUser = await googleAuthService.VerifyGoogleTokenAsync(dto.Token);
                if (googleUser == null)
                    return Results.BadRequest("Invalid Google token");

                var user = await db.Users.FirstOrDefaultAsync(u => u.GoogleId == googleUser.Id);
                if (user == null)
                {
                    user = await db.Users.FirstOrDefaultAsync(u => u.Email == googleUser.Email);
                    if (user != null && string.IsNullOrEmpty(user.GoogleId))
                    {
                        user.GoogleId = googleUser.Id;
                    }
                }
                if (user == null)
                {
                    user = new User
                    {
                        FirstName = googleUser.GivenName ?? "Google",
                        LastName = googleUser.FamilyName ?? "User",
                        Email = googleUser.Email,
                        PhoneNumber = "",
                        PasswordHash = "google_oauth",
                        GoogleId = googleUser.Id
                    };

                    db.Users.Add(user);
                    await db.SaveChangesAsync();
                }

                var jwtSettings = config.GetSection("Jwt");
                var keyString = jwtSettings["Key"];
                if (string.IsNullOrEmpty(keyString))
                    throw new Exception("JWT Key is missing in configuration");

                var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(keyString));
                var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

                var claims = new[]
                {
                    new Claim(ClaimTypes.NameIdentifier, user.UserId.ToString()),
                    new Claim(ClaimTypes.Email, user.Email),
                    new Claim(ClaimTypes.GivenName, user.FirstName),
                    new Claim(ClaimTypes.Surname, user.LastName),
                    new Claim("isGoogleUser", "true")
                };

                var token = new JwtSecurityToken(
                    issuer: jwtSettings["Issuer"],
                    audience: jwtSettings["Audience"],
                    claims: claims,
                    expires: DateTime.UtcNow.AddMinutes(Convert.ToDouble(jwtSettings["ExpireMinutes"])),
                    signingCredentials: creds
                );

                var tokenString = new JwtSecurityTokenHandler().WriteToken(token);

                return Results.Ok(new
                {
                    token = tokenString,
                    user = new
                    {
                        id = user.UserId,
                        firstName = user.FirstName,
                        lastName = user.LastName,
                        email = user.Email,
                        phoneNumber = user.PhoneNumber,
                        isGoogleUser = true
                    }
                });
            });
        }
    }

    public record RegisterDto(string FirstName, string LastName, string Email, string PhoneNumber, string Password);
    public record LoginDto(string? Email, string? PhoneNumber, string Password);
    public record GoogleAuthDto(string Token);
}