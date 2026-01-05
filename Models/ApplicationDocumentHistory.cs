using System.ComponentModel.DataAnnotations;

namespace MMGT.API.Models
{
    public class ApplicationDocumentHistory
    {
        public Guid Id { get; set; }

        // Nullable because an entry may be for a deleted document where the document id is known
        public Guid? ApplicationDocumentId { get; set; }

        public Guid ApplicationId { get; set; }

        [Required]
        [MaxLength(50)]
        public string Action { get; set; } = string.Empty; // Uploaded, Replaced, Deleted

        public Guid PerformedById { get; set; }

        public DateTime PerformedAt { get; set; } = DateTime.UtcNow;

        [MaxLength(255)]
        public string? OldFileName { get; set; }

        [MaxLength(255)]
        public string? NewFileName { get; set; }

        [MaxLength(1000)]
        public string? Comments { get; set; }

        // Navigation
        public virtual Application? Application { get; set; }
        public virtual ApplicationDocument? ApplicationDocument { get; set; }
    }
}
