using Microsoft.EntityFrameworkCore;
using DeBillPay_Backend.Models;

namespace DeBillPay_Backend.Data
{
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        public DbSet<User> Users { get; set; }
        public DbSet<Ebill> Ebills { get; set; }
        public DbSet<EbillParticipant> EbillParticipants { get; set; }
        public DbSet<Payment> Payments { get; set; }
        public DbSet<Comment> Comments { get; set; }
        public DbSet<Group> Groups { get; set; }
        public DbSet<GroupMember> GroupMembers { get; set; }
        public DbSet<Contact> Contacts { get; set; }
        public DbSet<Invitation> Invitations { get; set; }
        public DbSet<Notification> Notifications { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // USERS
            modelBuilder.Entity<User>()
                .HasMany(u => u.EbillsOrganized)
                .WithOne(e => e.Organizer)
                .HasForeignKey(e => e.OrganizerId)
                .OnDelete(DeleteBehavior.Restrict);

            // EBILL PARTICIPANTS
            modelBuilder.Entity<EbillParticipant>()
                .HasOne(p => p.User)
                .WithMany(u => u.EbillParticipants)
                .HasForeignKey(p => p.UserId);

            modelBuilder.Entity<EbillParticipant>()
                .HasOne(p => p.Ebill)
                .WithMany(e => e.Participants)
                .HasForeignKey(p => p.EbillId);

            // PAYMENTS
            modelBuilder.Entity<Payment>()
                .HasOne(p => p.User)
                .WithMany(u => u.Payments)
                .HasForeignKey(p => p.UserId);

            modelBuilder.Entity<Payment>()
                .HasOne(p => p.Ebill)
                .WithMany(e => e.Payments)
                .HasForeignKey(p => p.EbillId);

            // COMMENTS
            modelBuilder.Entity<Comment>()
                .HasOne(c => c.Ebill)
                .WithMany(e => e.Comments)
                .HasForeignKey(c => c.EbillId);

            // GROUPS
            modelBuilder.Entity<Group>()
                .HasOne(g => g.Owner)
                .WithMany(u => u.Groups)
                .HasForeignKey(g => g.UserId);

            // GROUP MEMBERS
            modelBuilder.Entity<GroupMember>()
                .HasOne(gm => gm.Group)
                .WithMany(g => g.Members)
                .HasForeignKey(gm => gm.GroupId);

            modelBuilder.Entity<GroupMember>()
                .HasOne(gm => gm.Member)
                .WithMany(u => u.GroupMemberships)
                .HasForeignKey(gm => gm.MemberId);

            // CONTACTS
            modelBuilder.Entity<Contact>()
                .HasOne(c => c.User)
                .WithMany(u => u.Contacts)
                .HasForeignKey(c => c.UserId);

            modelBuilder.Entity<Contact>()
                .HasOne(c => c.Friend)
                .WithMany()
                .HasForeignKey(c => c.FriendId);

            // INVITATIONS
            modelBuilder.Entity<Invitation>()
                .HasOne(i => i.Sender)
                .WithMany(u => u.SentInvitations)
                .HasForeignKey(i => i.SenderId);

            modelBuilder.Entity<Invitation>()
                .HasOne(i => i.Receiver)
                .WithMany(u => u.ReceivedInvitations)
                .HasForeignKey(i => i.ReceiverId);

            modelBuilder.Entity<Invitation>()
                .HasOne(i => i.Ebill)
                .WithMany(e => e.Invitations)
                .HasForeignKey(i => i.EbillId);

            // NOTIFICATIONS
            modelBuilder.Entity<Notification>()
                .HasOne(n => n.User)
                .WithMany(u => u.Notifications)
                .HasForeignKey(n => n.UserId);

            modelBuilder.Entity<Notification>()
                .HasOne(n => n.Ebill)
                .WithMany(e => e.Notifications)
                .HasForeignKey(n => n.EbillId);
        }
    }
}