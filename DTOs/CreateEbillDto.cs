namespace DeBillPay_Backend.DTOs
{
	public class CreateEbillDto
	{
		public string Name { get; set; } = null!;
		public string Currency { get; set; } = null!;
		public decimal AmountOfDept { get; set; }
		public string Description { get; set; } = null!;
		public string Scenario { get; set; } = null!;
		public List<int> ParticipantIds { get; set; } = new();
	}
}