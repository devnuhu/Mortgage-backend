using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MMGT.API.Models
{
    /// <summary>
    /// Audit log for document type changes.
    /// Tracks what changed, who changed it, and when (compliance requirement).
    /// </summary>
    public class DocumentTypeAuditLog
    {
        /// <summary>
        /// Primary key (GUID)
        /// </summary>
        public Guid Id { get; set; } = Guid.NewGuid();

        /// <summary>
        /// Document type that was changed
        /// </summary>
        [Required]
        public Guid DocumentTypeId { get; set; }

        /// <summary>
        /// Field that changed (e.g., "IsMandatory", "DisplayOrder", "IsActive")
        /// </summary>
        [Required]
        [MaxLength(100)]
        public string FieldName { get; set; } = string.Empty;

        /// <summary>
        /// Previous value (null if new record)
        /// </summary>
        [MaxLength(500)]
        public string? OldValue { get; set; }

        /// <summary>
        /// New value after change
        /// </summary>
        [Required]
        [MaxLength(500)]
        public string NewValue { get; set; } = string.Empty;

        /// <summary>
        /// Type of change: "Created", "Modified", "Deactivated"
        /// </summary>
        [Required]
        [MaxLength(50)]
        public string ChangeType { get; set; } = string.Empty;

        /// <summary>
        /// Reason for change (optional, but recommended for compliance)
        /// </summary>
        [MaxLength(500)]
        public string? ChangeReason { get; set; }

        /// <summary>
        /// Who made the change (User ID)
        /// </summary>
        [Required]
        public Guid ChangedBy { get; set; }

        /// <summary>
        /// When change occurred (UTC)
        /// </summary>
        [Required]
        public DateTime ChangedAt { get; set; } = DateTime.UtcNow;

        // Navigation properties
        [ForeignKey(nameof(DocumentTypeId))]
        public virtual DocumentType? DocumentType { get; set; }
    }
}
