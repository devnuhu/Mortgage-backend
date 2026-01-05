using System.ComponentModel.DataAnnotations;

namespace MMGT.API.Models
{
    public class Application
    {
        public Guid Id { get; set; }

        [Required]
        [MaxLength(50)]
        public string ApplicationNumber { get; set; } = string.Empty;

        [Required]
        [MaxLength(50)]
        public string Status { get; set; } = "draft"; // draft, submitted, under_review, approved, rejected, returned, completed

        [Required]
        [MaxLength(50)]
        public string Priority { get; set; } = "medium"; // low, medium, high, urgent

        // Essential Borrower Information
        [Required]
        [MaxLength(100)]
        public string FirstName { get; set; } = string.Empty;

        [MaxLength(100)]
        public string? MiddleName { get; set; }

        [Required]
        [MaxLength(100)]
        public string LastName { get; set; } = string.Empty;

        // RSA Pin (required for Nigerian mortgage)
        [Required]
        [MaxLength(15)]
        public string RsaPin { get; set; } = string.Empty;

        // Equity and Supplementary Values
        [Required]
        public decimal EquityValue { get; set; }

        [Required]
        public decimal SupplementaryValue { get; set; }

        // Loan Amount
        [Required]
        public decimal LoanAmount { get; set; }

        // Property Value
        [Required]
        public decimal PropertyValue { get; set; }

        // PMI Information (auto-populated)
        [Required]
        [MaxLength(200)]
        public string PmiRepName { get; set; } = string.Empty;

        [Required]
        [MaxLength(256)]
        public string PmiRepEmail { get; set; } = string.Empty;

        [Required]
        [MaxLength(200)]
        public string PmiCompanyName { get; set; } = string.Empty;

        // Timestamps
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        public DateTime? SubmittedAt { get; set; }

        // Foreign Keys
        public Guid UserId { get; set; }
        public virtual User User { get; set; } = null!;

        public Guid PmiCompanyId { get; set; } // Changed from PMICompanyId to PmiCompanyId
        public virtual PMICompany PmiCompany { get; set; } = null!; // Changed from PMICompany to PmiCompany

        // Navigation properties
        public virtual ICollection<ApplicationDocument> Documents { get; set; } = new List<ApplicationDocument>();
        public virtual ICollection<ApplicationReviewHistory> ReviewHistory { get; set; } = new List<ApplicationReviewHistory>();
    }
}