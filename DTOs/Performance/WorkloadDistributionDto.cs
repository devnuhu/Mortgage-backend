namespace MMGT.API.DTOs.Performance
{
    public class WorkloadDistributionDto
    {
        // Basic Reviewer Info
        public Guid ReviewerId { get; set; }
        public string ReviewerName { get; set; } = string.Empty;
        public string ReviewerEmail { get; set; } = string.Empty;

        #region Workload Breakdown
        public int ActiveAssignments { get; set; }
        public int PendingAssignments { get; set; }
        public int CompletedAssignments { get; set; }
        public int TotalAssignments { get; set; }
        public double WorkloadPercentage { get; set; }
        #endregion

        #region Performance Metrics
        public double ApprovalRatePercentage { get; set; }
        public double RejectionRatePercentage { get; set; }
        public double RevisionRequestRatePercentage { get; set; }
        public double AverageTurnaroundTimeHours { get; set; }
        public string PerformanceRating { get; set; } = string.Empty;
        #endregion

        #region Quality & Escalation
        public int EscalatedAssignments { get; set; }
        public double EscalationRatePercentage { get; set; }
        public double RevisionRequestRate { get; set; }
        public double ReworkPercentage { get; set; }
        public int DocumentsPending { get; set; }
        public int DocumentsApproved { get; set; }
        public int DocumentsRejected { get; set; }
        public int DocumentsRevisionRequested { get; set; }
        public double AverageDocumentsPerApplication { get; set; }
        #endregion

        #region Hours Tracking
        public double TotalHoursSpent { get; set; }
        public double AverageHoursPerAssignment { get; set; }
        public double EstimatedHoursRemaining { get; set; }
        public DateTime? EstimatedCompletionDate { get; set; }
        #endregion

        #region Capacity & Health
        public double MaxCapacityHours { get; set; }
        public double CapacityUtilizationPercentage { get; set; }
        public double AvailableCapacityHours { get; set; }
        public string CapacityStatus { get; set; } = "Normal"; // Green/Yellow/Red
        public string BurnoutRisk { get; set; } = "Low"; // Low/Medium/High
        public double SLAComplianceRate { get; set; }
        #endregion

        #region Trend & Velocity
        public double WeeklyWorkloadAverage { get; set; }
        public string WorkloadTrend { get; set; } = "Stable"; // Increasing/Stable/Decreasing
        public double AverageHoursLastWeek { get; set; }
        public double AverageHoursPreviousWeek { get; set; }
        public double VelocityChangePercentage { get; set; }
        public DateTime LastMetricsUpdateTime { get; set; }
        #endregion
    }

    public class WorkloadDistributionSummaryDto
    {
        #region Team Overview
        public int TotalReviewers { get; set; }
        public int TotalAssignments { get; set; }
        public int AverageAssignmentsPerReviewer { get; set; }
        public int MaxAssignments { get; set; }
        public int MinAssignments { get; set; }
        #endregion

        #region Team Performance
        public double OverallApprovalRate { get; set; }
        public double AverageEscalationRate { get; set; }
        public double TeamAverageTurnaroundTime { get; set; }
        public string OverallTeamHealth { get; set; } = "Good"; // Excellent/Good/Fair/Poor
        #endregion

        #region Team Hours & Capacity
        public double TotalTeamHoursSpent { get; set; }
        public double AverageHoursPerReviewer { get; set; }
        public double TeamCapacityUtilization { get; set; }
        public int ReviewersAtRisk { get; set; } // Count with High burnout risk
        public int ReviewersOverCapacity { get; set; } // Count over 100% utilization
        #endregion

        #region Team Trend
        public double TeamWeeklyVelocity { get; set; }
        public string TeamWorkloadTrend { get; set; } = "Stable"; // Increasing/Stable/Decreasing
        public double VelocityChangePercentage { get; set; }
        public int HighRiskReviewers { get; set; }
        #endregion

        public List<WorkloadDistributionDto> ReviewerWorkloads { get; set; } = new();
    }

    public class PaginatedWorkloadDistributionDto
    {
        public int PageNumber { get; set; }
        public int PageSize { get; set; }
        public int TotalCount { get; set; }
        public int TotalPages { get; set; }
        public bool HasPreviousPage { get; set; }
        public bool HasNextPage { get; set; }
        public WorkloadDistributionSummaryDto Summary { get; set; } = new();
        public List<WorkloadDistributionDto> ReviewerWorkloads { get; set; } = new();
    }
}
