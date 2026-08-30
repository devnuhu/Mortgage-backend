using System.ComponentModel.DataAnnotations;

namespace MMGT.API.Models
{
    public class User
    {
        public Guid Id { get; set; }

        [Required]
        [MaxLength(256)]
        public string Email { get; set; } = string.Empty;

        [Required]
        [MaxLength(100)]
        public string FirstName { get; set; } = string.Empty;

        [Required]
        [MaxLength(100)]
        public string LastName { get; set; } = string.Empty;

        [Required]
        [MaxLength(50)]
        public string Role { get; set; } = string.Empty; // SUPER_ADMIN, ADMIN, CLIENT, PMI, REVIEWER, LEAD_REVIEWER, SERVICE_MANAGER

        [Required]
        public string PasswordHash { get; set; } = string.Empty;

        public bool IsFirstLogin { get; set; } = true;
        public bool IsActive { get; set; } = true;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? LastLoginAt { get; set; }

        /// <summary>
        /// User's timezone offset in hours from UTC (e.g., 1 for Nigeria UTC+1, -5 for EST)
        /// Default: 1 (Nigeria). Auto-detected from browser/request when available.
        /// </summary>
        public int TimezoneOffsetHours { get; set; } = 1; // Default to Nigeria (UTC+1)

        // Navigation properties
        public Guid? PMICompanyId { get; set; }

        // 🆕 ADD THIS PROPERTY
        [MaxLength(256)]
        public string? PMIEmail { get; set; }

        public virtual PMICompany? PMICompany { get; set; }
        
        [MaxLength(128)]
        public string? FirstLoginTokenHash { get; set; }

        public DateTime? FirstLoginTokenExpiresAt { get; set; }

        // Navigation properties for new features
        public virtual ICollection<ApplicationAssignment> AssignedApplications { get; set; } = new HashSet<ApplicationAssignment>();
        public virtual ICollection<ApplicationAssignment> AssignmentsMadeByUser { get; set; } = new HashSet<ApplicationAssignment>();
        public virtual ICollection<ApplicationDocument> ReviewedDocuments { get; set; } = new HashSet<ApplicationDocument>();
        public virtual ICollection<DocumentType> DocumentTypesCreatedByUser { get; set; } = new HashSet<DocumentType>();
        public virtual ICollection<DocumentType> DocumentTypesModifiedByUser { get; set; } = new HashSet<DocumentType>();
        public virtual ICollection<DocumentTypeAuditLog> AuditLogsCreatedByUser { get; set; } = new HashSet<DocumentTypeAuditLog>();

        // Performance and escalation related navigation properties
        public virtual ICollection<ReviewerPerformanceMetrics>? PerformanceMetrics { get; set; }
        public virtual ICollection<EscalationRule>? EscalationRulesCreated { get; set; }
        public virtual ICollection<EscalationRule>? EscalationRulesModified { get; set; }
    }
}