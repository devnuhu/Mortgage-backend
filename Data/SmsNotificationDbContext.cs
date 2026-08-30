using Microsoft.EntityFrameworkCore;
using MMGT.API.Models;

namespace MMGT.API.Data
{
    /// <summary>
    /// Database context for SMSNotification Database
    /// Separate from ApplicationDbContext to isolate SMS processing database
    /// </summary>
    public class SmsNotificationDbContext : DbContext
    {
        public SmsNotificationDbContext(DbContextOptions<SmsNotificationDbContext> options)
            : base(options)
        {
        }

        public DbSet<Sms2Vendor> Sms2Vendors { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Configure Sms2Vendor entity to map to existing SMS2Vendor table
            modelBuilder.Entity<Sms2Vendor>(entity =>
            {
                entity.ToTable("SMS2Vendor");

                // Since table has no primary key, configure a key for EF Core tracking
                // but it won't be enforced in the database
                entity.HasKey(e => new { e.SenderID, e.PhoneNo });

                entity.Property(e => e.SenderID)
                    .IsRequired()
                    .HasMaxLength(50);

                entity.Property(e => e.PhoneNo)
                    .IsRequired()
                    .HasMaxLength(20);

                entity.Property(e => e.Message)
                    .IsRequired();

                entity.Property(e => e.SendStatus)
                    .HasMaxLength(50);
            });
        }
    }
}
