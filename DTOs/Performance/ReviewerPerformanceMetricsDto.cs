namespace MMGT.API.DTOs.Performance
{
    public class ReviewerPerformanceMetricsDto
    {
        public Guid Id { get; set; }
        public Guid ReviewerId { get; set; }
        public string ReviewerName { get; set; } = string.Empty;
        public int TotalAssignments { get; set; }
        public int ActiveAssignments { get; set; }
        public int CompletedAssignments { get; set; }
        public int DocumentsApproved { get; set; }
        public int DocumentsRejected { get; set; }
        public int DocumentsRevisionRequested { get; set; }
        public double AverageTurnaroundTimeHours { get; set; }
        public double ApprovalRatePercentage { get; set; }
        public double RejectionRatePercentage { get; set; }
        public double RevisionRequestRatePercentage { get; set; }
        public int EscalatedAssignments { get; set; }
        public int TotalEscalationCount { get; set; }
        public DateTime LastCalculatedAt { get; set; }
        public string PerformanceRating { get; set; } = string.Empty;
        public double AverageHoursPerAssignment { get; set; }
        public double TotalHoursSpent { get; set; }
    }
}
