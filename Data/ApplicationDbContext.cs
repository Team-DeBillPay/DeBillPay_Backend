using Microsoft.EntityFrameworkCore;
using DeBillPay_Backend.Models;

namespace DeBillPay_Backend.Data
{
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        public DbSet<Ebill> Ebills { get; set; }
    }
}