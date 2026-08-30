namespace MMGT.API.DTOs.Application
{
    public class ApplicationDto
    {
        public Guid Id { get; set; }
        public string ApplicationNumber { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public string Priority { get; set; } = string.Empty;

        // Essential Borrower Information
        public string FirstName { get; set; } = string.Empty;
        public string? MiddleName { get; set; }
        public string LastName { get; set; } = string.Empty;
        public string? PhoneNumber { get; set; }

        // RSA Pin
        public string RsaPin { get; set; } = string.Empty;

        // Equity and Supplementary Values
        public decimal EquityValue { get; set; }
        public decimal SupplementaryValue { get; set; }

        // Loan Amount
        public decimal LoanAmount { get; set; }

        // Property Value
        public decimal PropertyValue { get; set; }

        // PMI Information
        public string PmiRepName { get; set; } = string.Empty;
        public string PmiRepEmail { get; set; } = string.Empty;
        public string PmiCompanyName { get; set; } = string.Empty;

        // Timestamps
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        public DateTime? SubmittedAt { get; set; }

        // User and PMI Company Info
        public Guid UserId { get; set; }
        public string UserName { get; set; } = string.Empty;
        public Guid PmiCompanyId { get; set; }
        public string CompanyName { get; set; } = string.Empty; // Changed from PMICompanyName to avoid collision

        // Document count
        public int DocumentCount { get; set; }

        // Assigned Reviewer Information
        public string? AssignedReviewerName { get; set; }
        public string? AssignedReviewerEmail { get; set; }
        public DateTime? AssignedAt { get; set; }
    }
}