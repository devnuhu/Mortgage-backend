namespace MMGT.API.DTOs.Report
{
    public class ApplicationReportDto
    {
        public Guid Id { get; set; }
        public string ApplicationNumber { get; set; } = string.Empty;
        public string RsaPin { get; set; } = string.Empty;
        public string ApplicantName { get; set; } = string.Empty;
        public string PmiCompany { get; set; } = string.Empty;
        public decimal PropertyValue { get; set; }
        public decimal LoanAmount { get; set; }
        public string Status { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public DateTime? SubmittedAt { get; set; }
        public int DocumentCount { get; set; }
        // Decision information (filled when the application has been approved, rejected or returned)
        public DateTime? DecisionAt { get; set; }
        public double? TurnaroundDays { get; set; }
        public string? RejectionReason { get; set; }
        public DateTime? RejectedAt { get; set; }
        public string? RejectedBy { get; set; }

        // Assigned Reviewer Information
        public string? AssignedReviewerName { get; set; }
        public string? AssignedReviewerEmail { get; set; }
        public DateTime? AssignedAt { get; set; }
    }
}
