namespace MMGT.API.DTOs.Report
{
    public class ApplicationReportDto
    {
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

        // Assigned Reviewer Information
        public string? AssignedReviewerName { get; set; }
        public string? AssignedReviewerEmail { get; set; }
        public DateTime? AssignedAt { get; set; }
    }
}
