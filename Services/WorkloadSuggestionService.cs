using Microsoft.EntityFrameworkCore;
using MMGT.API.Data;
using MMGT.API.Services.Interfaces;

namespace MMGT.API.Services
{
    public class WorkloadSuggestionService : IWorkloadSuggestionService
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<WorkloadSuggestionService> _logger;
        private const double HighWorkloadThreshold = 0.75; // 75% of average

        public WorkloadSuggestionService(
            ApplicationDbContext context,
            ILogger<WorkloadSuggestionService> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<WorkloadBalancingSummaryDto> GetWorkloadBalancingSuggestionsAsync()
        {
            try
            {
                var activeAssignments = await _context.ApplicationAssignments
                    .AsNoTracking()
                    .Where(a => a.IsActive)
                    .Include(a => a.Application)
                    .Include(a => a.Reviewer)
                    .ToListAsync();

                if (activeAssignments.Count == 0)
                {
                    return new WorkloadBalancingSummaryDto
                    {
                        TotalApplications = 0,
                        SuggestionsGenerated = 0,
                        IsBalanced = true
                    };
                }

                // Group by reviewer and count assignments
                var reviewerWorkloads = activeAssignments
                    .GroupBy(a => a.ReviewerId)
                    .Select(g => new
                    {
                        ReviewerId = g.Key,
                        ReviewerName = g.First().Reviewer?.FirstName + " " + g.First().Reviewer?.LastName,
                        Workload = g.Count(),
                        Assignments = g.ToList()
                    })
                    .ToList();

                double avgWorkload = reviewerWorkloads.Average(r => r.Workload);
                double maxWorkload = reviewerWorkloads.Max(r => r.Workload);
                double minWorkload = reviewerWorkloads.Min(r => r.Workload);
                double stdDev = CalculateStandardDeviation(reviewerWorkloads.Select(r => (double)r.Workload).ToList());

                var suggestions = GenerateSuggestions(reviewerWorkloads.Cast<dynamic>().ToList(), avgWorkload);

                bool isBalanced = stdDev <= (avgWorkload * 0.2);

                return new WorkloadBalancingSummaryDto
                {
                    TotalApplications = activeAssignments.Count,
                    SuggestionsGenerated = suggestions.Count,
                    AverageWorkload = avgWorkload,
                    MaxWorkload = maxWorkload,
                    MinWorkload = minWorkload,
                    WorkloadStandardDeviation = stdDev,
                    Suggestions = suggestions,
                    IsBalanced = isBalanced
                };
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error generating workload balancing suggestions: {ex.Message}");
                throw;
            }
        }

        public async Task<List<WorkloadSuggestionDto>> GetSuggestionsForReviewerAsync(Guid reviewerId)
        {
            try
            {
                var activeAssignments = await _context.ApplicationAssignments
                    .AsNoTracking()
                    .Where(a => a.IsActive)
                    .Include(a => a.Application)
                    .Include(a => a.Reviewer)
                    .ToListAsync();

                var reviewerWorkloads = activeAssignments
                    .GroupBy(a => a.ReviewerId)
                    .Select(g => new
                    {
                        ReviewerId = g.Key,
                        ReviewerName = g.First().Reviewer?.FirstName + " " + g.First().Reviewer?.LastName,
                        Workload = g.Count(),
                        Assignments = g.ToList()
                    })
                    .ToList();

                double avgWorkload = reviewerWorkloads.Average(r => r.Workload);
                var suggestions = GenerateSuggestions(reviewerWorkloads.Cast<dynamic>().ToList(), avgWorkload);

                return suggestions.Where(s => s.CurrentReviewerId == reviewerId).ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error getting suggestions for reviewer {reviewerId}: {ex.Message}");
                throw;
            }
        }

        public async Task<List<WorkloadSuggestionDto>> GetReassignmentCandidatesAsync(Guid reviewerId, int count = 5)
        {
            try
            {
                var reviewerAssignments = await _context.ApplicationAssignments
                    .AsNoTracking()
                    .Where(a => a.ReviewerId == reviewerId && a.IsActive)
                    .Include(a => a.Application)
                    .Include(a => a.Reviewer)
                    .OrderBy(a => a.AssignedAt)
                    .Take(count)
                    .ToListAsync();

                var allAssignments = await _context.ApplicationAssignments
                    .AsNoTracking()
                    .Where(a => a.IsActive)
                    .Include(a => a.Reviewer)
                    .ToListAsync();

                var reviewerWorkloads = allAssignments
                    .GroupBy(a => a.ReviewerId)
                    .Select(g => new
                    {
                        ReviewerId = g.Key,
                        ReviewerName = g.First().Reviewer?.FirstName + " " + g.First().Reviewer?.LastName,
                        Workload = g.Count()
                    })
                    .OrderBy(r => r.Workload)
                    .ToList();

                double avgWorkload = reviewerWorkloads.Average(r => r.Workload);
                var leastBusyReviewer = reviewerWorkloads.FirstOrDefault();

                var suggestions = new List<WorkloadSuggestionDto>();

                foreach (var assignment in reviewerAssignments)
                {
                    var suggestion = new WorkloadSuggestionDto
                    {
                        ApplicationId = assignment.ApplicationId,
                        ApplicationRefNumber = assignment.Application?.ApplicationNumber ?? "N/A",
                        CurrentReviewerId = assignment.ReviewerId,
                        CurrentReviewerName = assignment.Reviewer?.FirstName + " " + assignment.Reviewer?.LastName,
                        CurrentReviewerWorkload = reviewerWorkloads.First(r => r.ReviewerId == reviewerId).Workload,
                        SuggestedReviewerId = leastBusyReviewer?.ReviewerId ?? Guid.Empty,
                        SuggestedReviewerName = leastBusyReviewer?.ReviewerName ?? "N/A",
                        SuggestedReviewerWorkload = leastBusyReviewer?.Workload ?? 0,
                        RebalanceReason = $"Reduce workload from {reviewerWorkloads.First(r => r.ReviewerId == reviewerId).Workload} to {avgWorkload:F0}",
                        ImprovementPercentage = ((reviewerWorkloads.First(r => r.ReviewerId == reviewerId).Workload - avgWorkload) / avgWorkload) * 100
                    };

                    suggestions.Add(suggestion);
                }

                return suggestions;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error getting reassignment candidates for reviewer {reviewerId}: {ex.Message}");
                throw;
            }
        }

        public async Task<bool> IsWorkloadBalancedAsync(double tolerancePercentage = 20)
        {
            try
            {
                var (stdDev, _) = await GetWorkloadImbalanceMetricsAsync();

                var activeAssignments = await _context.ApplicationAssignments
                    .AsNoTracking()
                    .Where(a => a.IsActive)
                    .CountAsync();

                var reviewerCount = await _context.ApplicationAssignments
                    .AsNoTracking()
                    .Where(a => a.IsActive)
                    .Select(a => a.ReviewerId)
                    .Distinct()
                    .CountAsync();

                if (reviewerCount == 0) return true;

                double avgWorkload = (double)activeAssignments / reviewerCount;
                double threshold = avgWorkload * (tolerancePercentage / 100);

                return stdDev <= threshold;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error checking workload balance: {ex.Message}");
                throw;
            }
        }

        public async Task<(double StdDev, double CoeffVariation)> GetWorkloadImbalanceMetricsAsync()
        {
            try
            {
                var reviewerWorkloads = await _context.ApplicationAssignments
                    .AsNoTracking()
                    .Where(a => a.IsActive)
                    .GroupBy(a => a.ReviewerId)
                    .Select(g => new { ReviewerId = g.Key, Workload = g.Count() })
                    .ToListAsync();

                if (reviewerWorkloads.Count == 0)
                    return (0, 0);

                var workloads = reviewerWorkloads.Select(r => (double)r.Workload).ToList();
                double stdDev = CalculateStandardDeviation(workloads);
                double mean = workloads.Average();
                double coeffVariation = mean > 0 ? (stdDev / mean) : 0;

                return (stdDev, coeffVariation);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error calculating workload imbalance metrics: {ex.Message}");
                throw;
            }
        }

        private List<WorkloadSuggestionDto> GenerateSuggestions(
            List<dynamic> reviewerWorkloads,
            double avgWorkload)
        {
            var suggestions = new List<WorkloadSuggestionDto>();

            var overloadedReviewers = reviewerWorkloads
                .Where(r => r.Workload > avgWorkload * HighWorkloadThreshold)
                .OrderByDescending(r => r.Workload)
                .ToList();

            var underloadedReviewers = reviewerWorkloads
                .Where(r => r.Workload < avgWorkload * 0.5)
                .OrderBy(r => r.Workload)
                .ToList();

            foreach (var overloaded in overloadedReviewers)
            {
                var underloaded = underloadedReviewers.FirstOrDefault();
                if (underloaded == null) break;

                var reassignmentCount = Math.Max(1, (int)(overloaded.Workload - avgWorkload) / 2);

                for (int i = 0; i < reassignmentCount && i < overloaded.Assignments.Count; i++)
                {
                    var assignment = overloaded.Assignments[i];

                    var suggestion = new WorkloadSuggestionDto
                    {
                        ApplicationId = assignment.ApplicationId,
                        ApplicationRefNumber = assignment.Application?.ApplicationNumber ?? "N/A",
                        CurrentReviewerId = overloaded.ReviewerId,
                        CurrentReviewerName = overloaded.ReviewerName,
                        CurrentReviewerWorkload = overloaded.Workload,
                        SuggestedReviewerId = underloaded.ReviewerId,
                        SuggestedReviewerName = underloaded.ReviewerName,
                        SuggestedReviewerWorkload = underloaded.Workload,
                        RebalanceReason = $"Reduce overloaded reviewer from {overloaded.Workload} to {avgWorkload:F0} items",
                        ImprovementPercentage = ((overloaded.Workload - avgWorkload) / avgWorkload) * 100
                    };

                    suggestions.Add(suggestion);
                }
            }

            return suggestions;
        }

        private double CalculateStandardDeviation(List<double> values)
        {
            if (values.Count == 0) return 0;

            double mean = values.Average();
            double sumOfSquaresOfDifferences = values.Sum(val => Math.Pow(val - mean, 2));
            double standardDeviation = Math.Sqrt(sumOfSquaresOfDifferences / values.Count);

            return standardDeviation;
        }
    }
}
