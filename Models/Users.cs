namespace DeBillPay_Backend.Models;

public class User
{
    public int UserId { get; set; }
    public string FirstName { get; set; } = null!;
    public string LastName { get; set; } = null!;
    public string Email { get; set; } = null!;
    public string PhoneNumber { get; set; } = null!;
    public string PasswordHash { get; set; } = null!;

    public ICollection<Ebill> EbillsOrganized { get; set; } = new List<Ebill>();
    public ICollection<EbillParticipant> EbillParticipants { get; set; } = new List<EbillParticipant>();
    public ICollection<Payment> Payments { get; set; } = new List<Payment>();
    public ICollection<Comment> Comments { get; set; } = new List<Comment>();
    public ICollection<Group> Groups { get; set; } = new List<Group>();
    public ICollection<GroupMember> GroupMemberships { get; set; } = new List<GroupMember>();
    public ICollection<Contact> Contacts { get; set; } = new List<Contact>();
    public ICollection<Invitation> SentInvitations { get; set; } = new List<Invitation>();
    public ICollection<Invitation> ReceivedInvitations { get; set; } = new List<Invitation>();
    public ICollection<Notification> Notifications { get; set; } = new List<Notification>();
}