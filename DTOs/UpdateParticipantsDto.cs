namespace DeBillPay_Backend.DTOs
{
public class AddParticipantsDto
{
    public List<int> UserIds { get; set; }
}
    public class UpdateParticipantDto
    {
        public int? ParticipantId { get; set; }
        public decimal? AssignedAmount { get; set; }
        public decimal? PaidAmount { get; set; }
        public decimal? AmountOfDept { get; set; }
    }

}