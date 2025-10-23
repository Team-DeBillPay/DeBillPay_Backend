using System.ComponentModel.DataAnnotations;
using DeBillPay_Backend.Models.Validation;

namespace DeBillPay_Backend.DTOs
{
    public class UpdateUserDto
    {
        [StringLength(50, MinimumLength = 1)]
        public string? FirstName { get; set; }

        [StringLength(50, MinimumLength = 1)]
        public string? LastName { get; set; }

        [EmailAddress]
        public string? Email { get; set; }

        [UkrainianPhone]
        public string? PhoneNumber { get; set; }

        [MinLength(6)]
        public string? Password { get; set; }
    }
}