using System.ComponentModel.DataAnnotations;
namespace DeBillPay_Backend.Models;


public class EbillParticipant
{
    [Key]
    public int ParticipantId { get; set; }
	public decimal AssignedAmount { get; set; }
	public string PaymentStatus { get; set; } = null!;
	public bool IsAdminRights { get; set; }
	public decimal Balance { get; set; }

	public int UserId { get; set; }
	public User User { get; set; } = null!;

	public int EbillId { get; set; }
	public Ebill Ebill { get; set; } = null!;
}