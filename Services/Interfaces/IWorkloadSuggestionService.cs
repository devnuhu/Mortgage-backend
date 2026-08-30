using MMGT.API.DTOs.Performance;

namespace MMGT.API.Services.Interfaces
{
    public class WorkloadSuggestionDto
    {
        public Guid ApplicationId { get; set; }
        public string ApplicationRefNumber { get; set; }
        public Guid CurrentReviewerId { get; set; }
        public string CurrentReviewerName { get; set; }
        public int CurrentReviewerWorkload { get; set; }
        public Guid SuggestedReviewerId { get; set; }
        public string SuggestedReviewerName { get; set; }
        public int SuggestedReviewerWorkload { get; set; }
        public string RebalanceReason { get; set; }
        public double ImprovementPercentage { get; set; }
    }

    public class WorkloadBalancingSummaryDto
    {
        public int TotalApplications { get; set; }
        public int SuggestionsGenerated { get; set; }
        public double AverageWorkload { get; set; }
        public double MaxWorkload { get; set; }
        public double MinWorkload { get; set; }
        public double WorkloadStandardDeviation { get; set; }
        public List<WorkloadSuggestionDto> Suggestions { get; set; } = new();
        public bool IsBalanced { get; set; }
    }

    public interface IWorkloadSuggestionService
    {
        /// <summary>
        /// Analyze current workload distribution and generate rebalancing suggestions
        /// </summary>
        Task<WorkloadBalancingSummaryDto> GetWorkloadBalancingSuggestionsAsync();

        /// <summary>
        /// Get rebalancing suggestions for a specific reviewer
        /// </summary>
        Task<List<WorkloadSuggestionDto>> GetSuggestionsForReviewerAsync(Guid reviewerId);

        /// <summary>
        /// Get applications that should be reassigned from a reviewer
        /// </summary>
        Task<List<WorkloadSuggestionDto>> GetReassignmentCandidatesAsync(Guid reviewerId, int count = 5);

        /// <summary>
        /// Check if workload is balanced across reviewers
        /// </summary>
        Task<bool> IsWorkloadBalancedAsync(double tolerancePercentage = 20);

        /// <summary>
        /// Get workload imbalance metrics
        /// </summary>
        Task<(double StdDev, double CoeffVariation)> GetWorkloadImbalanceMetricsAsync();
    }
}
