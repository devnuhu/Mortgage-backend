namespace MMGT.API.DTOs.Application
{
    /// <summary>
    /// DTO for search results showing essential fields including status and reviewer comments
    /// </summary>
    public class ApplicationSearchDto
    {
        public Guid Id { get; set; }

        public string ApplicationNumber { get; set; } = string.Empty;

        public string Status { get; set; } = string.Empty;

        // Applicant Information
        public string FirstName { get; set; } = string.Empty;

        public string? MiddleName { get; set; }

        public string LastName { get; set; } = string.Empty;

        public string? PhoneNumber { get; set; }

        public string RsaPin { get; set; } = string.Empty;

        // Financial Details
        public decimal PropertyValue { get; set; }

        public decimal LoanAmount { get; set; }

        public decimal EquityValue { get; set; }

        public decimal SupplementaryValue { get; set; }

        // PMI Information
        public string? PmiCompanyName { get; set; }

        public string? PmiRepName { get; set; }

        public string? PmiRepEmail { get; set; }

        // User/Reviewer Information
        public Guid UserId { get; set; }

        public string UserName { get; set; } = string.Empty;

        // Document Count
        public int DocumentCount { get; set; }

        // Timestamps
        public DateTime CreatedAt { get; set; }

        public DateTime? SubmittedAt { get; set; }

        public DateTime UpdatedAt { get; set; }

        // Review Information
        public string? LatestReviewComment { get; set; }

        public string? LatestReviewerName { get; set; }

        public DateTime? LatestReviewDate { get; set; }
    }
}
