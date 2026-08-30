using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MMGT.API.Models
{
    /// <summary>
    /// Represents a document uploaded for an application.
    /// Supports both legacy file-based storage (FilePath) and new database storage (FileContent).
    /// Implements versioning and per-document review status tracking.
    /// </summary>
    public class ApplicationDocument
    {
        /// <summary>
        /// Primary key (GUID)
        /// </summary>
        public Guid Id { get; set; }

        /// <summary>
        /// Original file name (immutable)
        /// e.g., "passport_scan.pdf", "proof_of_income.pdf"
        /// </summary>
        [Required]
        [MaxLength(255)]
        public string FileName { get; set; } = string.Empty;

        /// <summary>
        /// File path for legacy file-based storage (wwwroot/uploads)
        /// Populated for old applications, null for new database-stored documents
        /// </summary>
        [MaxLength(500)]
        public string? FilePath { get; set; }

        /// <summary>
        /// File content stored in database (VARBINARY)
        /// Only populated for new documents. Old documents use FilePath only.
        /// </summary>
        public byte[]? FileContent { get; set; }

        /// <summary>
        /// MIME type (e.g., "application/pdf", "image/jpeg")
        /// </summary>
        [Required]
        [MaxLength(100)]
        public string FileType { get; set; } = string.Empty;

        /// <summary>
        /// Foreign key to DocumentType
        /// e.g., GUID for "NATIONAL_IDENTITY_CARD" DocumentType
        /// </summary>
        [Required]
        public Guid DocumentTypeId { get; set; }

        /// <summary>
        /// Backward compatibility: legacy DocumentType string field
        /// Will be deprecated; use DocumentTypeId + DocumentType navigation instead
        /// </summary>
        [MaxLength(100)]
        [Obsolete("Use DocumentTypeId instead")]
        public string? LegacyDocumentType { get; set; }

        /// <summary>
        /// File size in bytes
        /// </summary>
        public long FileSize { get; set; }

        /// <summary>
        /// Document version number (auto-increment on replacement)
        /// Allows tracking multiple versions of the same logical document
        /// </summary>
        [Required]
        public int Version { get; set; } = 1;

        /// <summary>
        /// Is this the latest version of this document?
        /// Constraint: Only one document per application+documentTypeId can have IsLatestVersion=true
        /// </summary>
        [Required]
        public bool IsLatestVersion { get; set; } = true;

        /// <summary>
        /// Review status of this document
        /// Values: "Pending", "Approved", "Rejected", "RequestedRevision"
        /// </summary>
        [Required]
        [MaxLength(50)]
        public string ReviewStatus { get; set; } = "Pending";

        /// <summary>
        /// If rejected/revision, reason why
        /// </summary>
        [MaxLength(500)]
        public string? ReviewComments { get; set; }

        /// <summary>
        /// Who reviewed this document (Reviewer User ID)
        /// Null if ReviewStatus is "Pending"
        /// </summary>
        public Guid? ReviewedBy { get; set; }

        /// <summary>
        /// When was this document reviewed? (UTC)
        /// </summary>
        public DateTime? ReviewedAt { get; set; }

        /// <summary>
        /// When document was uploaded (UTC)
        /// </summary>
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Who uploaded this document (User ID of client)
        /// </summary>
        [Required]
        public Guid UploadedBy { get; set; }

        /// <summary>
        /// Indicates if this is a legacy document from the old disk storage system
        /// Legacy documents are READ-ONLY: Cannot be replaced or deleted
        /// New documents (BLOB-stored) can be replaced and deleted
        /// </summary>
        [Required]
        public bool IsLegacy { get; set; } = false;

        // Foreign Keys
        [Required]
        public Guid ApplicationId { get; set; }

        // Navigation properties
        public virtual Application Application { get; set; } = null!;

        [ForeignKey(nameof(DocumentTypeId))]
        public virtual DocumentType? DocumentType { get; set; }

        [ForeignKey(nameof(ReviewedBy))]
        public virtual User? Reviewer { get; set; }
    }
}