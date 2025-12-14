using System.Collections.Generic;
namespace DeBillPay_Backend.DTOs
{
    public class ParticipantAmountDto
    {
        public int UserId { get; set; }

        public decimal? Amount { get; set; }

        public decimal PaidAmount { get; set; } = 0;
    }
}