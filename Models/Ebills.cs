namespace DeBillPay_Backend.Models;

public class Ebill
{
    public int EbillId { get; set; }
    public string Name { get; set; } = null!;
    public string Currency { get; set; } = null!;
    public decimal AmountOfDept { get; set; }
    public string Description { get; set; } = null!;
    public string Scenario { get; set; } = null!;
    public string Status { get; set; } = null!;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public int OrganizerId { get; set; }
    public User Organizer { get; set; } = null!;

    public ICollection<EbillParticipant> Participants { get; set; } = new List<EbillParticipant>();
    public ICollection<Payment> Payments { get; set; } = new List<Payment>();
    public ICollection<Comment> Comments { get; set; } = new List<Comment>();
    public ICollection<Invitation> Invitations { get; set; } = new List<Invitation>();
    public ICollection<Notification> Notifications { get; set; } = new List<Notification>();
}