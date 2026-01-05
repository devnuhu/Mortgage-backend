using System.ComponentModel.DataAnnotations;

namespace MMGT.API.Models
{
    public class ApplicationReviewHistory
    {
        public Guid Id { get; set; }

        [Required]
        [MaxLength(50)]
        public string Status { get; set; } = string.Empty; // submitted, under_review, approved, rejected, returned, completed

        [MaxLength(1000)]
        public string? Comments { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // Foreign Keys
        public Guid ApplicationId { get; set; }
        public virtual Application Application { get; set; } = null!;

        public Guid ReviewedById { get; set; }
        public virtual User ReviewedBy { get; set; } = null!;
    }
}