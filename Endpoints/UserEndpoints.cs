using Microsoft.AspNetCore.Cryptography.KeyDerivation;
using System.Security.Cryptography;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using DeBillPay_Backend.Data;
using DeBillPay_Backend.Models;
using Microsoft.EntityFrameworkCore;

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

                var salt = RandomNumberGenerator.GetBytes(16);
                var hash = Convert.ToBase64String(KeyDerivation.Pbkdf2(
                    password: dto.Password,
                    salt: salt,
                    prf: KeyDerivationPrf.HMACSHA256,
                    iterationCount: 100000,
                    numBytesRequested: 32));

                var user = new User
                {
                    FirstName = dto.FirstName,
                    LastName = dto.LastName,
                    Email = dto.Email,
                    PhoneNumber = dto.PhoneNumber,
                    PasswordHash = $"{Convert.ToBase64String(salt)}.{hash}"
                };

                db.Users.Add(user);
                await db.SaveChangesAsync();

                return Results.Ok("User registered successfully");
            });


            app.MapPost("/api/auth/login", async (ApplicationDbContext db, IConfiguration config, LoginDto dto) =>
            {
                var user = await db.Users.FirstOrDefaultAsync(u => u.Email == dto.Email);
                if (user == null)
                    return Results.BadRequest("Invalid email or password");

                var parts = user.PasswordHash.Split('.');
                if (parts.Length != 2)
                    return Results.BadRequest("Password format invalid");

                var salt = Convert.FromBase64String(parts[0]);
                var storedHash = parts[1];
                var enteredHash = Convert.ToBase64String(KeyDerivation.Pbkdf2(
                    password: dto.Password,
                    salt: salt,
                    prf: KeyDerivationPrf.HMACSHA256,
                    iterationCount: 100000,
                    numBytesRequested: 32));

                if (storedHash != enteredHash)
                    return Results.BadRequest("Invalid email or password");


                var jwtSettings = config.GetSection("Jwt");
                var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSettings["Key"]));
                var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

                var claims = new[]
                {
                    new Claim(ClaimTypes.NameIdentifier, user.UserId.ToString()),
                    new Claim(ClaimTypes.Email, user.Email)
                };

                var token = new JwtSecurityToken(
                    issuer: jwtSettings["Issuer"],
                    audience: jwtSettings["Audience"],
                    claims: claims,
                    expires: DateTime.UtcNow.AddMinutes(Convert.ToDouble(jwtSettings["ExpireMinutes"])),
                    signingCredentials: creds
                );

                var tokenString = new JwtSecurityTokenHandler().WriteToken(token);
                return Results.Ok(new { token = tokenString });
            });
        }
    }

    public record RegisterDto(string FirstName, string LastName, string Email, string PhoneNumber, string Password);
    public record LoginDto(string Email, string Password);
}