using System.Collections.Generic;
namespace DeBillPay_Backend.DTOs
{
    public class ParticipantAmountDto
    {
        public int UserId { get; set; }

        // якщо ≥ндив≥дуальн≥ Ч Amount обовТ€зковий
        public decimal? Amount { get; set; }

        // якщо сп≥льн≥ витрати Ч це сума фактично оплачена користувачем
        public decimal PaidAmount { get; set; } = 0;
    }
}