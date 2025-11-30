namespace DeBillPay_Backend.DTOs
{
public class AddParticipantsDto
{
        public List<int> UserIds { get; set; } = new List<int>();
    }
    public class UpdateParticipantDto
    {
        public string? Name { get; set; }
        public string? Description { get; set; }
        public int? ParticipantId { get; set; }
        public decimal? AssignedAmount { get; set; }
        public decimal? PaidAmount { get; set; }
        public decimal? AmountOfDept { get; set; }
    }

}