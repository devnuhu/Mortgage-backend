namespace MMGT.API.DTOs.Application
{
    /// <summary>
    /// DTO for complete application details including review history and assignments
    /// Used by SERVICE_MANAGER to view any application, or by others to view their own
    /// </summary>
    public class ApplicationDetailsDto
    {
        public Guid Id { get; set; }

        public string ApplicationNumber { get; set; } = string.Empty;

        public string Status { get; set; } = string.Empty;

        public string Priority { get; set; } = string.Empty;

        // Essential Applicant Information
        public string FirstName { get; set; } = string.Empty;

        public string? MiddleName { get; set; }

        public string LastName { get; set; } = string.Empty;

        public string? PhoneNumber { get; set; }

        public string RsaPin { get; set; } = string.Empty;

        // Financial Details
        public decimal EquityValue { get; set; }

        public decimal SupplementaryValue { get; set; }

        public decimal LoanAmount { get; set; }

        public decimal PropertyValue { get; set; }

        // PMI Information
        public Guid? PmiCompanyId { get; set; }

        public string? PmiCompanyName { get; set; }

        public string? PmiRepName { get; set; }

        public string? PmiRepEmail { get; set; }

        // User Information
        public Guid UserId { get; set; }

        public string UserName { get; set; } = string.Empty;

        public string UserEmail { get; set; } = string.Empty;

        // Document Information
        public int DocumentCount { get; set; }

        public List<ApplicationDocumentDetailDto> Documents { get; set; } = new();

        // Timestamps
        public DateTime CreatedAt { get; set; }

        public DateTime? SubmittedAt { get; set; }

        public DateTime UpdatedAt { get; set; }

        // Review History
        public List<ReviewHistoryDetailDto> ReviewHistory { get; set; } = new();

        // Assignment Information
        public ApplicationAssignmentDetailDto? CurrentAssignment { get; set; }

        public List<ApplicationAssignmentDetailDto> AssignmentHistory { get; set; } = new();
    }

    /// <summary>
    /// Document detail for ApplicationDetailsDto
    /// </summary>
    public class ApplicationDocumentDetailDto
    {
        public Guid Id { get; set; }

        public string DocumentType { get; set; } = string.Empty;

        public string FileName { get; set; } = string.Empty;

        public string? ReviewStatus { get; set; }

        public string? ReviewerComments { get; set; }

        public string? ReviewerName { get; set; }

        public DateTime? ReviewedAt { get; set; }

        public DateTime UploadedAt { get; set; }
    }

    /// <summary>
    /// Review history detail for ApplicationDetailsDto
    /// </summary>
    public class ReviewHistoryDetailDto
    {
        public Guid Id { get; set; }

        public string Status { get; set; } = string.Empty;

        public string UpdatedStatus { get; set; } = string.Empty;

        public string? ReviewComments { get; set; }

        public string ReviewerName { get; set; } = string.Empty;

        public string ReviewerEmail { get; set; } = string.Empty;

        public DateTime ReviewedAt { get; set; }
    }

    /// <summary>
    /// Assignment detail for ApplicationDetailsDto
    /// </summary>
    public class ApplicationAssignmentDetailDto
    {
        public Guid Id { get; set; }

        public string ReviewerName { get; set; } = string.Empty;

        public string ReviewerEmail { get; set; } = string.Empty;

        public string? ReassignmentReason { get; set; }

        public DateTime AssignedAt { get; set; }

        public bool IsActive { get; set; }
    }
}
