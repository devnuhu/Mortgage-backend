using System.ComponentModel.DataAnnotations;

namespace MMGT.API.Models
{
    public class ApplicationDocument
    {
        public Guid Id { get; set; }

        [Required]
        [MaxLength(255)]
        public string FileName { get; set; } = string.Empty;

        [Required]
        [MaxLength(500)]
        public string FilePath { get; set; } = string.Empty;

        [Required]
        [MaxLength(100)]
        public string FileType { get; set; } = string.Empty; // MIME type

        [Required]
        [MaxLength(100)]
        public string DocumentType { get; set; } = string.Empty; // identity_document, income_proof, property_document, etc.

        public long FileSize { get; set; } // in bytes

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // Foreign Key
        public Guid ApplicationId { get; set; }
        public virtual Application Application { get; set; } = null!;
    }
}