using Microsoft.EntityFrameworkCore;
using MMGT.API.Models;

namespace MMGT.API.Data
{
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        public DbSet<User> Users { get; set; }
        public DbSet<PMICompany> PMICompanies { get; set; }
        public DbSet<Application> Applications { get; set; }
        public DbSet<ApplicationDocument> ApplicationDocuments { get; set; }
        public DbSet<ApplicationReviewHistory> ApplicationReviewHistory { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Configure User entity
            modelBuilder.Entity<User>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Email).IsRequired().HasMaxLength(256);
                entity.HasIndex(e => e.Email).IsUnique();
                entity.Property(e => e.Role).IsRequired().HasMaxLength(50);
                entity.Property(e => e.FirstName).IsRequired().HasMaxLength(100);
                entity.Property(e => e.LastName).IsRequired().HasMaxLength(100);
                entity.Property(e => e.PasswordHash).IsRequired();
                entity.Property(e => e.CreatedAt).HasDefaultValueSql("GETDATE()");
                entity.Property(e => e.UpdatedAt).HasDefaultValueSql("GETDATE()");

                // Relationship with PMICompany
                entity.HasOne(e => e.PMICompany)
                    .WithMany(p => p.Users)
                    .HasForeignKey(e => e.PMICompanyId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            // Configure PMICompany entity
            modelBuilder.Entity<PMICompany>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.PmiName).IsRequired().HasMaxLength(200);
                entity.Property(e => e.EmailAddress).IsRequired().HasMaxLength(256);
                entity.HasIndex(e => e.EmailAddress).IsUnique();
                entity.Property(e => e.ResourcePersonName).IsRequired().HasMaxLength(200);
                entity.Property(e => e.ResourcePersonEmail).IsRequired().HasMaxLength(256);
                entity.Property(e => e.PhoneNumber).IsRequired().HasMaxLength(20);
                entity.Property(e => e.Status).IsRequired().HasMaxLength(50);
                entity.Property(e => e.CreatedAt).HasDefaultValueSql("GETDATE()");
                entity.Property(e => e.UpdatedAt).HasDefaultValueSql("GETDATE()");
            });

            // Configure Application entity
            modelBuilder.Entity<Application>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.ApplicationNumber).IsRequired().HasMaxLength(50);
                entity.HasIndex(e => e.ApplicationNumber).IsUnique();
                entity.Property(e => e.Status).IsRequired().HasMaxLength(50);
                entity.Property(e => e.Priority).IsRequired().HasMaxLength(50);

                // Borrower Information
                entity.Property(e => e.FirstName).IsRequired().HasMaxLength(100);
                entity.Property(e => e.MiddleName).HasMaxLength(100);
                entity.Property(e => e.LastName).IsRequired().HasMaxLength(100);
                entity.Property(e => e.RsaPin).IsRequired().HasMaxLength(15);

                // Decimal properties with precision
                entity.Property(e => e.EquityValue).HasPrecision(18, 2);
                entity.Property(e => e.SupplementaryValue).HasPrecision(18, 2);
                entity.Property(e => e.LoanAmount).HasPrecision(18, 2);
                entity.Property(e => e.PropertyValue).HasPrecision(18, 2);

                // PMI Information
                entity.Property(e => e.PmiRepName).IsRequired().HasMaxLength(200);
                entity.Property(e => e.PmiRepEmail).IsRequired().HasMaxLength(256);
                entity.Property(e => e.PmiCompanyName).IsRequired().HasMaxLength(200);

                entity.Property(e => e.CreatedAt).HasDefaultValueSql("GETDATE()");
                entity.Property(e => e.UpdatedAt).HasDefaultValueSql("GETDATE()");

                // Relationships
                entity.HasOne(e => e.User)
                    .WithMany()
                    .HasForeignKey(e => e.UserId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(e => e.PmiCompany)
                    .WithMany()
                    .HasForeignKey(e => e.PmiCompanyId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            // Configure ApplicationDocument entity
            modelBuilder.Entity<ApplicationDocument>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.FileName).IsRequired().HasMaxLength(255);
                entity.Property(e => e.FilePath).IsRequired().HasMaxLength(500);
                entity.Property(e => e.FileType).IsRequired().HasMaxLength(100);
                entity.Property(e => e.DocumentType).IsRequired().HasMaxLength(100);
                entity.Property(e => e.CreatedAt).HasDefaultValueSql("GETDATE()");

                // Relationships
                entity.HasOne(e => e.Application)
                    .WithMany(a => a.Documents)
                    .HasForeignKey(e => e.ApplicationId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            // Configure ApplicationReviewHistory entity
            modelBuilder.Entity<ApplicationReviewHistory>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Status).IsRequired().HasMaxLength(50);
                entity.Property(e => e.Comments).HasMaxLength(1000);
                entity.Property(e => e.CreatedAt).HasDefaultValueSql("GETDATE()");

                // Relationships
                entity.HasOne(e => e.Application)
                    .WithMany(a => a.ReviewHistory)
                    .HasForeignKey(e => e.ApplicationId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(e => e.ReviewedBy)
                    .WithMany()
                    .HasForeignKey(e => e.ReviewedById)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            // Configure ApplicationDocumentHistory entity
            modelBuilder.Entity<ApplicationDocumentHistory>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Action).IsRequired().HasMaxLength(50);
                entity.Property(e => e.PerformedAt).HasDefaultValueSql("GETDATE()");
                entity.Property(e => e.OldFileName).HasMaxLength(255);
                entity.Property(e => e.NewFileName).HasMaxLength(255);
                entity.Property(e => e.Comments).HasMaxLength(1000);

                entity.HasOne(e => e.Application)
                    .WithMany()
                    .HasForeignKey(e => e.ApplicationId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(e => e.ApplicationDocument)
                    .WithMany()
                    .HasForeignKey(e => e.ApplicationDocumentId)
                    .OnDelete(DeleteBehavior.Restrict);
            });
        }
    }
}