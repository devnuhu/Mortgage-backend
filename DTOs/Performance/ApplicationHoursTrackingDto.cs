namespace MMGT.API.DTOs.Performance
{
    /// <summary>
    /// DTO for tracking hours spent on a specific application by reviewers.
    /// Used for audit trail and compliance reporting.
    /// </summary>
    public class ApplicationHoursTrackingDto
    {
        public Guid ApplicationId { get; set; }
        public string ApplicationNumber { get; set; } = string.Empty;
        public string ApplicantName { get; set; } = string.Empty;
        public DateTime AssignedAt { get; set; }
        public DateTime? CompletedAt { get; set; }
        public Guid ReviewerId { get; set; }
        public string ReviewerName { get; set; } = string.Empty;
        public string ReviewerEmail { get; set; } = string.Empty;
        public double HoursSpent { get; set; }
        public int DocumentsReviewed { get; set; }
        public int DocumentsApproved { get; set; }
        public int DocumentsRejected { get; set; }
        public int DocumentsRevisionRequested { get; set; }
    }

    /// <summary>
    /// Aggregated hours data per reviewer for audit compliance reporting.
    /// </summary>
    public class ReviewerHoursAuditSummaryDto
    {
        public Guid ReviewerId { get; set; }
        public string ReviewerName { get; set; } = string.Empty;
        public string ReviewerEmail { get; set; } = string.Empty;
        public DateTime? DateFrom { get; set; }
        public DateTime? DateTo { get; set; }
        public double TotalHoursSpent { get; set; }
        public int TotalApplicationsProcessed { get; set; }
        public int TotalDocumentsReviewed { get; set; }
        public double AverageHoursPerApplication { get; set; }
        public List<ApplicationHoursTrackingDto> ApplicationDetails { get; set; } = new();
    }
}
