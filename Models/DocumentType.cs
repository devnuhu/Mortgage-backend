using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MMGT.API.Models
{
    /// <summary>
    /// Represents a configurable document type required for mortgage applications.
    /// SUPER_ADMIN can add, edit, deactivate document types without code deployment.
    /// </summary>
    public class DocumentType
    {
        /// <summary>
        /// Primary key (GUID)
        /// </summary>
        public Guid Id { get; set; } = Guid.NewGuid();

        /// <summary>
        /// Unique code identifier (immutable after creation)
        /// e.g., "ACCESS_ARM_APPLICATION_FORM", "NATIONAL_IDENTITY_CARD"
        /// </summary>
        [Required]
        [MaxLength(100)]
        public string Code { get; set; } = string.Empty;

        /// <summary>
        /// User-friendly display name
        /// e.g., "ACCESS-ARM APPLICATION FORM (PROPERLY SIGNED)"
        /// </summary>
        [Required]
        [MaxLength(255)]
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Help text/description for clients and reviewers
        /// </summary>
        [MaxLength(1000)]
        public string? Description { get; set; }

        /// <summary>
        /// Whether this document is mandatory for application submission
        /// 21 mandatory, 3 optional (default configuration)
        /// </summary>
        [Required]
        public bool IsMandatory { get; set; } = true;

        /// <summary>
        /// Display order in UI (ascending)
        /// </summary>
        [Required]
        public int DisplayOrder { get; set; } = 0;

        /// <summary>
        /// Maximum file size allowed in KB (default 500KB)
        /// </summary>
        [Required]
        public int MaxFileSizeKB { get; set; } = 500;

        /// <summary>
        /// Soft delete flag (inactive = hidden from new applications)
        /// </summary>
        [Required]
        public bool IsActive { get; set; } = true;

        /// <summary>
        /// Created timestamp (UTC)
        /// </summary>
        [Required]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Created by (User ID)
        /// </summary>
        [Required]
        public Guid CreatedBy { get; set; }

        /// <summary>
        /// Last modified timestamp (UTC)
        /// </summary>
        public DateTime? ModifiedAt { get; set; }

        /// <summary>
        /// Last modified by (User ID)
        /// </summary>
        public Guid? ModifiedBy { get; set; }

        // Navigation properties
        public virtual ICollection<ApplicationDocument> ApplicationDocuments { get; set; } = new HashSet<ApplicationDocument>();
        public virtual ICollection<DocumentTypeAuditLog> AuditLogs { get; set; } = new HashSet<DocumentTypeAuditLog>();
    }
}
