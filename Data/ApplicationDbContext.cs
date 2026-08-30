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
        public DbSet<ApplicationDocumentHistory> ApplicationDocumentHistory { get; set; }
        public DbSet<DocumentType> DocumentTypes { get; set; }
        public DbSet<DocumentTypeAuditLog> DocumentTypeAuditLogs { get; set; }
        public DbSet<ApplicationAssignment> ApplicationAssignments { get; set; }
        public DbSet<SystemConfiguration> SystemConfigurations { get; set; }
        public DbSet<ReviewerPerformanceMetrics> ReviewerPerformanceMetrics { get; set; }
        public DbSet<EscalationRule> EscalationRules { get; set; }
        public DbSet<AssignmentEscalation> AssignmentEscalations { get; set; }
        public DbSet<ReviewerAvailability> ReviewerAvailabilities { get; set; }
        public DbSet<ReassignmentHistory> ReassignmentHistories { get; set; }
        public DbSet<RoleLoginAccessControl> RoleLoginAccessControls { get; set; }

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
                entity.HasIndex(e => e.Status);
                entity.Property(e => e.Priority).IsRequired().HasMaxLength(50);

                // Borrower Information
                entity.Property(e => e.FirstName).IsRequired().HasMaxLength(100);
                entity.Property(e => e.MiddleName).HasMaxLength(100);
                entity.Property(e => e.LastName).IsRequired().HasMaxLength(100);
                entity.Property(e => e.PhoneNumber).HasMaxLength(20);
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
                    .WithMany(p => p.Applications)
                    .HasForeignKey(e => e.PmiCompanyId)
                    .OnDelete(DeleteBehavior.Restrict);

                // RsaPin must be globally unique - can NEVER be reused
                // Unique constraint enforced at application level by CheckRsaPinExistsAsync
                // entity.HasIndex(e => e.RsaPin).IsUnique();

                // Additional performance indexes
                entity.HasIndex(e => new { e.UserId, e.CreatedAt }).IsDescending(false, true);
                entity.HasIndex(e => new { e.PmiCompanyId, e.CreatedAt }).IsDescending(false, true);
                entity.HasIndex(e => new { e.Status, e.CreatedAt }).IsDescending(false, true);
                entity.HasIndex(e => e.CreatedAt).IsDescending(true);
            });

            // Configure ApplicationDocument entity
            modelBuilder.Entity<ApplicationDocument>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.FileName).IsRequired().HasMaxLength(255);
                entity.Property(e => e.FilePath).HasMaxLength(500);
                entity.Property(e => e.FileType).IsRequired().HasMaxLength(100);
                entity.Property(e => e.ReviewStatus).IsRequired().HasMaxLength(50);
                entity.Property(e => e.ReviewComments).HasMaxLength(500);
                entity.Property(e => e.CreatedAt).HasDefaultValueSql("GETDATE()");
                entity.Property(e => e.Version).IsRequired();
                entity.Property(e => e.IsLatestVersion).IsRequired();
                entity.Property(e => e.LegacyDocumentType).HasMaxLength(100);

                // Relationships
                entity.HasOne(e => e.Application)
                    .WithMany(a => a.Documents)
                    .HasForeignKey(e => e.ApplicationId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(e => e.DocumentType)
                    .WithMany(dt => dt.ApplicationDocuments)
                    .HasForeignKey(e => e.DocumentTypeId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(e => e.Reviewer)
                    .WithMany(u => u.ReviewedDocuments)
                    .HasForeignKey(e => e.ReviewedBy)
                    .OnDelete(DeleteBehavior.Restrict);

                // Index: Latest version per application+documentType
                entity.HasIndex(e => new { e.ApplicationId, e.DocumentTypeId, e.IsLatestVersion });
                entity.HasIndex(e => new { e.ApplicationId, e.ReviewStatus });
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

            // Configure DocumentType entity
            modelBuilder.Entity<DocumentType>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Code).IsRequired().HasMaxLength(100);
                entity.HasIndex(e => e.Code).IsUnique();
                entity.Property(e => e.Name).IsRequired().HasMaxLength(255);
                entity.Property(e => e.Description).HasMaxLength(1000);
                entity.Property(e => e.IsMandatory).IsRequired();
                entity.Property(e => e.DisplayOrder).IsRequired();
                entity.Property(e => e.MaxFileSizeKB).IsRequired();
                entity.Property(e => e.IsActive).IsRequired();
                entity.Property(e => e.CreatedAt).HasDefaultValueSql("GETDATE()");
                entity.Property(e => e.CreatedBy).IsRequired();
                entity.Property(e => e.ModifiedAt);
                entity.Property(e => e.ModifiedBy);

                // Relationships: User who created this document type
                entity.HasOne<User>()
                    .WithMany(u => u.DocumentTypesCreatedByUser)
                    .HasForeignKey(e => e.CreatedBy)
                    .OnDelete(DeleteBehavior.Restrict);

                // Relationships: User who last modified this document type
                entity.HasOne<User>()
                    .WithMany(u => u.DocumentTypesModifiedByUser)
                    .HasForeignKey(e => e.ModifiedBy)
                    .OnDelete(DeleteBehavior.Restrict);

                // Index: Active documents sorted by display order
                entity.HasIndex(e => new { e.IsActive, e.DisplayOrder });
            });

            // Configure DocumentTypeAuditLog entity
            modelBuilder.Entity<DocumentTypeAuditLog>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.DocumentTypeId).IsRequired();
                entity.Property(e => e.FieldName).IsRequired().HasMaxLength(100);
                entity.Property(e => e.OldValue).HasMaxLength(500);
                entity.Property(e => e.NewValue).IsRequired().HasMaxLength(500);
                entity.Property(e => e.ChangeType).IsRequired().HasMaxLength(50);
                entity.Property(e => e.ChangeReason).HasMaxLength(500);
                entity.Property(e => e.ChangedAt).HasDefaultValueSql("GETDATE()");

                // Relationships
                entity.HasOne(e => e.DocumentType)
                    .WithMany(dt => dt.AuditLogs)
                    .HasForeignKey(e => e.DocumentTypeId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne<User>()
                    .WithMany(u => u.AuditLogsCreatedByUser)
                    .HasForeignKey(e => e.ChangedBy)
                    .OnDelete(DeleteBehavior.Restrict);

                // Index: Audit trail by DocumentTypeId and date
                entity.HasIndex(e => new { e.DocumentTypeId, e.ChangedAt }).IsDescending(false, true);
            });

            // Configure ApplicationAssignment entity
            modelBuilder.Entity<ApplicationAssignment>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.ApplicationId).IsRequired();
                entity.Property(e => e.ReviewerId).IsRequired();
                entity.Property(e => e.AssignedAt).HasDefaultValueSql("GETDATE()");
                entity.Property(e => e.ReassignmentReason).HasMaxLength(500);
                entity.Property(e => e.IsActive).IsRequired();

                // Relationships
                entity.HasOne(e => e.Application)
                    .WithMany(a => a.Assignments)
                    .HasForeignKey(e => e.ApplicationId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(e => e.Reviewer)
                    .WithMany(u => u.AssignedApplications)
                    .HasForeignKey(e => e.ReviewerId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(e => e.AssignedByUser)
                    .WithMany(u => u.AssignmentsMadeByUser)
                    .HasForeignKey(e => e.AssignedBy)
                    .OnDelete(DeleteBehavior.Restrict);

                // Index: Current assignments (speed up "my applications" queries)
                entity.HasIndex(e => new { e.ReviewerId, e.IsActive });
                // Index: Active assignment history
                entity.HasIndex(e => new { e.ApplicationId, e.IsActive });
            });

            // Configure SystemConfiguration entity
            modelBuilder.Entity<SystemConfiguration>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.ConfigKey).IsRequired().HasMaxLength(100);
                entity.HasIndex(e => e.ConfigKey).IsUnique();
                entity.Property(e => e.ConfigValue).IsRequired().HasMaxLength(500);
                entity.Property(e => e.Description).HasMaxLength(500);
                entity.Property(e => e.LastModifiedAt).HasDefaultValueSql("GETDATE()");
            });

            // Configure ReviewerPerformanceMetrics entity
            modelBuilder.Entity<ReviewerPerformanceMetrics>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.ReviewerId).IsRequired();
                entity.Property(e => e.TotalAssignments).IsRequired();
                entity.Property(e => e.ActiveAssignments).IsRequired();
                entity.Property(e => e.CompletedAssignments).IsRequired();
                entity.Property(e => e.DocumentsApproved).IsRequired();
                entity.Property(e => e.DocumentsRejected).IsRequired();
                entity.Property(e => e.DocumentsRevisionRequested).IsRequired();
                entity.Property(e => e.AverageTurnaroundTimeHours).IsRequired();
                entity.Property(e => e.ApprovalRatePercentage).IsRequired();
                entity.Property(e => e.RejectionRatePercentage).IsRequired();
                entity.Property(e => e.RevisionRequestRatePercentage).IsRequired();
                entity.Property(e => e.EscalatedAssignments).IsRequired();
                entity.Property(e => e.TotalEscalationCount).IsRequired();
                entity.Property(e => e.LastCalculatedAt).IsRequired();
                entity.Property(e => e.PerformanceRating).IsRequired().HasMaxLength(50);
                entity.Property(e => e.TotalHoursSpent).IsRequired();
                entity.Property(e => e.AverageHoursPerAssignment).IsRequired();

                entity.HasOne(e => e.Reviewer)
                    .WithMany()
                    .HasForeignKey(e => e.ReviewerId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasIndex(e => e.ReviewerId).IsUnique();
                entity.HasIndex(e => new { e.ApprovalRatePercentage, e.LastCalculatedAt }).IsDescending(true, true);
            });

            // Configure EscalationRule entity
            modelBuilder.Entity<EscalationRule>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.RuleName).IsRequired().HasMaxLength(200);
                entity.Property(e => e.Description).HasMaxLength(500);
                entity.Property(e => e.ThresholdHours).IsRequired();
                entity.Property(e => e.ApplicablePriority).IsRequired().HasMaxLength(50);
                entity.Property(e => e.EscalationAction).IsRequired().HasMaxLength(50);
                entity.Property(e => e.NotifyEmails).HasMaxLength(500);
                entity.Property(e => e.IsActive).IsRequired();
                entity.Property(e => e.CreatedBy).IsRequired();
                entity.Property(e => e.CreatedAt).IsRequired();
                entity.Property(e => e.ModifiedAt);
                entity.Property(e => e.ModifiedBy);

                entity.HasOne(e => e.CreatedByUser)
                    .WithMany(u => u.EscalationRulesCreated)
                    .HasForeignKey(e => e.CreatedBy)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(e => e.ModifiedByUser)
                    .WithMany(u => u.EscalationRulesModified)
                    .HasForeignKey(e => e.ModifiedBy)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasIndex(e => e.IsActive);
                entity.HasIndex(e => new { e.ApplicablePriority, e.IsActive });
            });

            // Configure AssignmentEscalation entity
            modelBuilder.Entity<AssignmentEscalation>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.ApplicationAssignmentId).IsRequired();
                entity.Property(e => e.EscalationRuleId).IsRequired();
                entity.Property(e => e.EscalatedAt).IsRequired();
                entity.Property(e => e.Status).IsRequired().HasMaxLength(50);
                entity.Property(e => e.HoursOverThreshold).IsRequired();
                entity.Property(e => e.ActionReason).HasMaxLength(500);
                entity.Property(e => e.ResolvedAt);
                entity.Property(e => e.ResolutionComments).HasMaxLength(1000);

                entity.HasOne(e => e.ApplicationAssignment)
                    .WithMany()
                    .HasForeignKey(e => e.ApplicationAssignmentId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(e => e.EscalationRule)
                    .WithMany(r => r.Escalations)
                    .HasForeignKey(e => e.EscalationRuleId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(e => e.ReassignedToReviewer)
                    .WithMany()
                    .HasForeignKey(e => e.ReassignedToReviewerId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(e => e.ResolvedByUser)
                    .WithMany()
                    .HasForeignKey(e => e.ResolvedBy)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasIndex(e => new { e.ApplicationAssignmentId, e.Status });
                entity.HasIndex(e => new { e.EscalationRuleId, e.Status });
                entity.HasIndex(e => e.Status);
                entity.HasIndex(e => e.EscalatedAt).IsDescending(true);
            });

            // Configure ReassignmentHistory entity
            modelBuilder.Entity<ReassignmentHistory>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.ApplicationAssignmentId).IsRequired();
                entity.Property(e => e.ApplicationId).IsRequired();
                entity.Property(e => e.PreviousReviewerId).IsRequired();
                entity.Property(e => e.NewReviewerId).IsRequired();
                entity.Property(e => e.ReassignmentReason).IsRequired().HasMaxLength(50);
                entity.Property(e => e.ReassignmentDetails).HasMaxLength(500);
                entity.Property(e => e.ReassignedAt).IsRequired();
                entity.Property(e => e.PreviousAssignmentStatus).HasMaxLength(50);
                entity.Property(e => e.NewAssignmentStatus).HasMaxLength(50);
                entity.Property(e => e.IsCompleted).IsRequired();

                // Relationships - cascade through ApplicationAssignment to Application
                entity.HasOne(e => e.ApplicationAssignment)
                    .WithMany()
                    .HasForeignKey(e => e.ApplicationAssignmentId)
                    .OnDelete(DeleteBehavior.Cascade);

                // Application reference uses NoAction to prevent cascade cycles
                entity.HasOne<Application>()
                    .WithMany()
                    .HasForeignKey(e => e.ApplicationId)
                    .OnDelete(DeleteBehavior.NoAction);

                // User references - preserve history for audit trail
                entity.HasOne(e => e.PreviousReviewer)
                    .WithMany()
                    .HasForeignKey(e => e.PreviousReviewerId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(e => e.NewReviewer)
                    .WithMany()
                    .HasForeignKey(e => e.NewReviewerId)
                    .OnDelete(DeleteBehavior.NoAction);

                entity.HasOne(e => e.ReassignedByUser)
                    .WithMany()
                    .HasForeignKey(e => e.ReassignedByUserId)
                    .OnDelete(DeleteBehavior.Restrict);

                // Indexes for performance
                entity.HasIndex(e => e.ApplicationAssignmentId);
                entity.HasIndex(e => e.ApplicationId);
                entity.HasIndex(e => e.PreviousReviewerId);
                entity.HasIndex(e => e.NewReviewerId);
                entity.HasIndex(e => e.ReassignedAt).IsDescending(true);
            });

            // Configure RoleLoginAccessControl entity
            modelBuilder.Entity<RoleLoginAccessControl>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Role).IsRequired().HasMaxLength(50);
                entity.HasIndex(e => e.Role).IsUnique();
                entity.Property(e => e.IsLoginEnabled).IsRequired();
                entity.Property(e => e.DisabledMessage).HasMaxLength(500);
                entity.Property(e => e.ChangeReason).HasMaxLength(1000);
                entity.Property(e => e.IsScheduled).IsRequired();
                entity.Property(e => e.ScheduledReason).HasMaxLength(1000);
                entity.Property(e => e.LastModifiedBy).IsRequired();
                entity.Property(e => e.LastModifiedAt).HasDefaultValueSql("GETDATE()");
                entity.Property(e => e.CreatedAt).HasDefaultValueSql("GETDATE()");

                // Index for checking active schedules
                entity.HasIndex(e => new { e.IsScheduled, e.ScheduledStartTime, e.ScheduledEndTime });
            });
        }
    }

}