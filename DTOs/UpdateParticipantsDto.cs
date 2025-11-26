namespace DeBillPay_Backend.DTOs
{
    public class AddParticipantDto
    {
        public int UserId { get; set; }

    }
    public class UpdateParticipantDto
    {
        public int? ParticipantId { get; set; }
        public decimal? AssignedAmount { get; set; }
        public decimal? PaidAmount { get; set; }
        public decimal? AmountOfDept { get; set; }
    }

}