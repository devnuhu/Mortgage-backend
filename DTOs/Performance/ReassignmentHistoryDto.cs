namespace MMGT.API.DTOs.Performance
{
    public class ReassignmentHistoryDto
    {
        public Guid Id { get; set; }
        public Guid ApplicationId { get; set; }
        public string ApplicationRefNumber { get; set; }
        public Guid ApplicationAssignmentId { get; set; }
        public Guid PreviousReviewerId { get; set; }
        public string PreviousReviewerName { get; set; }
        public Guid NewReviewerId { get; set; }
        public string NewReviewerName { get; set; }
        public string ReassignmentReason { get; set; }
        public string ReassignmentDetails { get; set; }
        public Guid? ReassignedByUserId { get; set; }
        public string ReassignedByUserName { get; set; }
        public DateTime ReassignedAt { get; set; }
        public string PreviousAssignmentStatus { get; set; }
        public string NewAssignmentStatus { get; set; }
        public bool IsCompleted { get; set; }
    }

    public class ReassignmentAuditMetricsDto
    {
        public int TotalReassignments { get; set; }
        public int ManualReassignments { get; set; }
        public int AutomaticReassignments { get; set; }
        public int ReassignmentsFromUnavailability { get; set; }
        public int WorkloadBalancingReassignments { get; set; }
        public int PerformanceIssueReassignments { get; set; }
        public Dictionary<string, int> ReassignmentsByReason { get; set; } = new();
        public List<ReassignmentHistoryDto> RecentReassignments { get; set; } = new();
        public double AverageReassignmentsPerReviewer { get; set; }
        public Dictionary<Guid, int> ReassignmentCountByReviewer { get; set; } = new();
    }
}
