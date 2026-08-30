namespace MMGT.API.DTOs.Workload
{
    public class ReassignmentCriteriaRequest
    {
        /// <summary>
        /// Reassign if current reviewer's workload exceeds this percentage of average
        /// </summary>
        public double WorkloadThresholdPercentage { get; set; } = 75;

        /// <summary>
        /// Reassign if reviewer is unavailable or on leave
        /// </summary>
        public bool ReassignIfUnavailable { get; set; } = true;

        /// <summary>
        /// Only reassign to reviewers with workload below this percentage of average
        /// </summary>
        public double TargetWorkloadMaxPercentage { get; set; } = 50;

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
    }
}
