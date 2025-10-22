using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using DeBillPay_Backend.Endpoints;
using DeBillPay_Backend.Data;
using Microsoft.EntityFrameworkCore;
var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

var jwtSettings = builder.Configuration.GetSection("Jwt");
var secretKey = builder.Configuration["Jwt:Key"];
if (string.IsNullOrEmpty(secretKey))
    throw new Exception("JWT secret key is missing in configuration.");

var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey));

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtSettings["Issuer"],
            ValidAudience = jwtSettings["Audience"],
            IssuerSigningKey = key
        };
    });

builder.Services.AddAuthorization();


var app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "DeBillPay API v1");
    c.RoutePrefix = "swagger"; 
});

app.UseAuthentication();
app.UseAuthorization();

app.MapUserEndpoints();

app.Run();