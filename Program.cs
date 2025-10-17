using DeBillPay_Backend.Data;
using Microsoft.EntityFrameworkCore;
using DeBillPay_Backend.Services;
using DeBillPay_Backend.Models;
using DeBillPay_Backend.Endpoints;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

var app = builder.Build();
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    Console.WriteLine($"Connection string: {builder.Configuration.GetConnectionString("DefaultConnection")}");
    db.Database.Migrate(); // автоматично створює або оновлює базу
}
app.UseHttpsRedirection();
app.UseCors();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}


app.MapEbillEndpoints();

app.Run();