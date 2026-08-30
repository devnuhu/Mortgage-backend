using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MMGT.API.Models
{
    /// <summary>
    /// Stores aggregated performance metrics for reviewers.
    /// Calculated daily or on-demand by ReviewerPerformanceService.
    /// Used by LEAD_REVIEWER to monitor reviewer performance and set escalation rules.
    /// </summary>
    public class ReviewerPerformanceMetrics
    {
        /// <summary>
        /// Primary key (GUID)
        /// </summary>
        public Guid Id { get; set; } = Guid.NewGuid();

        /// <summary>
        /// Reviewer whose metrics are being tracked
        /// </summary>
        [Required]
        public Guid ReviewerId { get; set; }

        /// <summary>
        /// Total applications assigned to this reviewer (all-time)
        /// </summary>
        [Required]
        public int TotalAssignments { get; set; } = 0;

        /// <summary>
        /// Applications currently assigned and active (pending review)
        /// </summary>
        [Required]
        public int ActiveAssignments { get; set; } = 0;

        /// <summary>
        /// Applications completed by this reviewer (all-time)
        /// </summary>
        [Required]
        public int CompletedAssignments { get; set; } = 0;

        /// <summary>
        /// Total documents approved (all-time)
        /// </summary>
        [Required]
        public int DocumentsApproved { get; set; } = 0;

        /// <summary>
        /// Total documents rejected (all-time)
        /// </summary>
        [Required]
        public int DocumentsRejected { get; set; } = 0;

        /// <summary>
        /// Total documents requiring revision (all-time)
        /// </summary>
        [Required]
        public int DocumentsRevisionRequested { get; set; } = 0;

        /// <summary>
        /// Average turnaround time in hours for completed assignments
        /// Calculated as average (CompletedAt - AssignedAt) for all completed assignments
        /// </summary>
        [Required]
        public double AverageTurnaroundTimeHours { get; set; } = 0.0;

        /// <summary>
        /// Approval rate percentage (DocumentsApproved / (DocumentsApproved + DocumentsRejected + DocumentsRevisionRequested))
        /// </summary>
        [Required]
        public double ApprovalRatePercentage { get; set; } = 0.0;

        /// <summary>
        /// Rejection rate percentage (DocumentsRejected / total reviews)
        /// </summary>
        [Required]
        public double RejectionRatePercentage { get; set; } = 0.0;

        /// <summary>
        /// Revision request rate percentage (DocumentsRevisionRequested / total reviews)
        /// </summary>
        [Required]
        public double RevisionRequestRatePercentage { get; set; } = 0.0;

        /// <summary>
        /// Number of applications currently on escalation (delayed beyond threshold)
        /// </summary>
        [Required]
        public int EscalatedAssignments { get; set; } = 0;

        /// <summary>
        /// Count of escalations triggered for this reviewer (all-time)
        /// </summary>
        [Required]
        public int TotalEscalationCount { get; set; } = 0;

        /// <summary>
        /// Last date metrics were calculated/updated
        /// </summary>
        [Required]
        public DateTime LastCalculatedAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Performance rating: "Excellent" (>90%), "Good" (75-90%), "Average" (50-75%), "Poor" (<50%)
        /// Based on approval rate and turnaround time
        /// </summary>
        [Required]
        [MaxLength(50)]
        public string PerformanceRating { get; set; } = "Average";

        /// <summary>
        /// Total hours spent on all completed assignments
        /// </summary>
        [Required]
        public double TotalHoursSpent { get; set; } = 0.0;

        /// <summary>
        /// Average hours per completed assignment (TotalHoursSpent / CompletedAssignments)
        /// </summary>
        [Required]
        public double AverageHoursPerAssignment { get; set; } = 0.0;

        // Navigation property
        [ForeignKey(nameof(ReviewerId))]
        public virtual User? Reviewer { get; set; }
    }
}
