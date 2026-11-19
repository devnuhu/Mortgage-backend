namespace MMGT.API.Services.Interfaces
{
    public class CriteriaReassignmentResultDto
    {
        public Guid ApplicationId { get; set; }
        public string ApplicationRefNumber { get; set; }
        public Guid PreviousReviewerId { get; set; }
        public string PreviousReviewerName { get; set; }
        public Guid NewReviewerId { get; set; }
        public string NewReviewerName { get; set; }
        public string ReassignmentReason { get; set; }
        public bool Success { get; set; }
        public string ErrorMessage { get; set; }
    }

    public class ReassignmentCriteriaDto
    {
        /// <summary>
        /// Reassign if current reviewer's workload exceeds this percentage of average
        /// </summary>
        public double WorkloadThresholdPercentage { get; set; } = 75; // 75% above average

        /// <summary>
        /// Reassign if reviewer is unavailable or on leave
        /// </summary>
        public bool ReassignIfUnavailable { get; set; } = true;

        /// <summary>
        /// Only reassign to reviewers with workload below this percentage of average
        /// </summary>
        public double TargetWorkloadMaxPercentage { get; set; } = 50; // 50% of average

        /// <summary>
        /// Prioritize reviewers with high performance ratings
        /// </summary>
        public bool PrioritizeHighPerformers { get; set; } = true;

        /// <summary>
        /// Minimum approval rate for target reviewers
        /// </summary>
        public double MinimumApprovalRatePercentage { get; set; } = 70;

        /// <summary>
        /// Maximum number of applications to reassign
        /// </summary>
        public int MaxReassignments { get; set; } = 20;
        /// <summary>Largest difference in open items allowed between reviewers before work is moved.</summary>
        public int MaxDifference { get; set; } = 3;

        /// <summary>Only work out the moves and return them; do not make them.</summary>
        public bool PreviewOnly { get; set; } = false;
    }

    public interface ICriteriaBasedReassignmentService
    {
        /// <summary>
        /// Execute reassignments based on predefined criteria
        /// </summary>
        Task<List<CriteriaReassignmentResultDto>> ExecuteReassignmentsByCriteriaAsync(ReassignmentCriteriaDto criteria, Guid? performedBy = null);

        /// <summary>
        /// Get applications that meet reassignment criteria for a specific reviewer
        /// </summary>
        Task<List<CriteriaReassignmentResultDto>> GetReassignmentCandidatesByCriteriaAsync(Guid reviewerId, ReassignmentCriteriaDto criteria);

        /// <summary>
        /// Reassign all applications from unavailable reviewers
        /// </summary>
        Task<List<CriteriaReassignmentResultDto>> ReassignFromUnavailableReviewersAsync();

        /// <summary>
        /// Auto-balance workload across all reviewers
        /// </summary>
        Task<List<CriteriaReassignmentResultDto>> AutoBalanceWorkloadAsync(Guid? performedBy = null);

        /// <summary>
        /// Validate if a reassignment would violate any rules
        /// </summary>
        Task<(bool IsValid, string Reason)> ValidateReassignmentAsync(Guid applicationId, Guid currentReviewerId, Guid newReviewerId);
    }
}
