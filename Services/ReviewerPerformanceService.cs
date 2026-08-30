using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using MMGT.API.Data;
using MMGT.API.Models;
using MMGT.API.DTOs.Performance;

namespace MMGT.API.Services
{
    public interface IReviewerPerformanceService
    {
        Task<ReviewerPerformanceMetricsDto> GetReviewerMetricsAsync(Guid reviewerId);
        Task<List<ReviewerPerformanceMetricsDto>> GetAllReviewersMetricsAsync();
        Task RecalculateMetricsAsync(Guid reviewerId);
        Task RecalculateAllMetricsAsync();
        Task<double> GetAverageTurnaroundTimeAsync(Guid reviewerId, int days = 30);
        Task<List<ReviewerPerformanceMetricsDto>> GetTopPerformersAsync(int count = 5);
        Task<List<ReviewerPerformanceMetricsDto>> GetBottomPerformersAsync(int count = 5);
        Task<WorkloadDistributionSummaryDto> GetWorkloadDistributionAsync();
        Task<PaginatedWorkloadDistributionDto> GetWorkloadDistributionPagedAsync(
            int pageNumber = 1,
            int pageSize = 25,
            string sortBy = "ReviewerName",
            bool sortDescending = false);
        Task<List<ReassignmentHistoryDto>> GetReassignmentHistoryAsync(int days = 30, int pageSize = 100);
        Task<List<ReassignmentHistoryDto>> GetReassignmentHistoryForReviewerAsync(Guid reviewerId, int days = 30);
        Task<ReassignmentAuditMetricsDto> GetReassignmentMetricsAsync();
        Task<List<ApplicationHoursTrackingDto>> GetApplicationHoursTrackingAsync(Guid applicationId);
        Task<ReviewerHoursAuditSummaryDto> GetReviewerHoursAuditSummaryAsync(Guid reviewerId, DateTime? dateFrom = null, DateTime? dateTo = null);
    }

    public class ReviewerPerformanceService : IReviewerPerformanceService
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<ReviewerPerformanceService> _logger;
        private readonly IMemoryCache _memoryCache;

        // Cache keys
        private const string ALL_METRICS_CACHE_KEY = "performance_all_reviewers_metrics";
        private const string TOP_PERFORMERS_CACHE_KEY_PREFIX = "performance_top_performers_";
        private const string BOTTOM_PERFORMERS_CACHE_KEY_PREFIX = "performance_bottom_performers_";
        private const string WORKLOAD_DISTRIBUTION_CACHE_KEY = "performance_workload_distribution";
        private const int CACHE_DURATION_MINUTES = 5;

        public ReviewerPerformanceService(
            ApplicationDbContext context,
            ILogger<ReviewerPerformanceService> logger,
            IMemoryCache memoryCache)
        {
            _context = context;
            _logger = logger;
            _memoryCache = memoryCache;
        }

        /// <summary>
        /// Get performance metrics for a specific reviewer
        /// </summary>
        public async Task<ReviewerPerformanceMetricsDto> GetReviewerMetricsAsync(Guid reviewerId)
        {
            var metrics = await _context.ReviewerPerformanceMetrics
                .AsNoTracking()
                .Include(m => m.Reviewer)
                .FirstOrDefaultAsync(m => m.ReviewerId == reviewerId);

            if (metrics == null)
            {
                // If metrics don't exist, calculate them
                await RecalculateMetricsAsync(reviewerId);
                metrics = await _context.ReviewerPerformanceMetrics
                    .Include(m => m.Reviewer)
                    .FirstOrDefaultAsync(m => m.ReviewerId == reviewerId);
            }

            return metrics == null ? new ReviewerPerformanceMetricsDto() : MapToDto(metrics);
        }

        /// <summary>
        /// Get performance metrics for all reviewers
        /// </summary>
        public async Task<List<ReviewerPerformanceMetricsDto>> GetAllReviewersMetricsAsync()
        {
            if (_memoryCache.TryGetValue(ALL_METRICS_CACHE_KEY, out List<ReviewerPerformanceMetricsDto> cachedMetrics))
            {
                return cachedMetrics;
            }

            var metrics = await _context.ReviewerPerformanceMetrics
                .AsNoTracking()
                .Include(m => m.Reviewer)
                .Where(m => m.Reviewer != null && m.Reviewer.IsActive)
                .OrderByDescending(m => m.ApprovalRatePercentage)
                .ToListAsync();

            var result = metrics.Select(MapToDto).ToList();

            var cacheOptions = new MemoryCacheEntryOptions()
                .SetSize(result.Count)
                .SetAbsoluteExpiration(TimeSpan.FromMinutes(CACHE_DURATION_MINUTES));
            _memoryCache.Set(ALL_METRICS_CACHE_KEY, result, cacheOptions);

            return result;
        }

        /// <summary>
        /// Recalculate performance metrics for a specific reviewer
        /// </summary>
        public async Task RecalculateMetricsAsync(Guid reviewerId)
        {
            try
            {
                var reviewer = await _context.Users.FindAsync(reviewerId);
                if (reviewer == null)
                {
                    _logger.LogWarning($"Reviewer {reviewerId} not found for metrics calculation");
                    return;
                }

                // Get all assignments for this reviewer
                var assignments = await _context.ApplicationAssignments
                    .Where(a => a.ReviewerId == reviewerId)
                    .Include(a => a.Application)
                    .ToListAsync();

                var activeAssignments = assignments.Where(a => a.IsActive).Count();
                var totalAssignments = assignments.Count;

                // Calculate completed assignments with turnaround time
                var completedAssignments = new List<ApplicationAssignment>();
                var totalHours = 0.0;

                foreach (var assignment in assignments.Where(a => !a.IsActive))
                {
                    if (assignment.Application != null && assignment.Application.CompletedAt.HasValue)
                    {
                        completedAssignments.Add(assignment);
                        var hours = (assignment.Application.CompletedAt.Value - assignment.AssignedAt).TotalHours;
                        totalHours += hours;
                    }
                }

                var avgTurnaroundHours = completedAssignments.Any() 
                    ? totalHours / completedAssignments.Count 
                    : 0.0;

                // Get document review statistics
                var reviewedDocuments = await _context.ApplicationDocuments
                    .Where(d => d.ReviewedBy == reviewerId)
                    .ToListAsync();

                var approved = reviewedDocuments.Count(d => d.ReviewStatus == "approved");
                var rejected = reviewedDocuments.Count(d => d.ReviewStatus == "rejected");
                var revisionRequested = reviewedDocuments.Count(d => d.ReviewStatus == "revision_requested");

                var totalReviewed = approved + rejected + revisionRequested;
                var approvalRate = totalReviewed > 0 ? (approved * 100.0 / totalReviewed) : 0.0;
                var rejectionRate = totalReviewed > 0 ? (rejected * 100.0 / totalReviewed) : 0.0;
                var revisionRate = totalReviewed > 0 ? (revisionRequested * 100.0 / totalReviewed) : 0.0;

                // Count escalations
                var escalations = await _context.AssignmentEscalations
                    .Where(e => e.ApplicationAssignment!.ReviewerId == reviewerId)
                    .ToListAsync();

                var activeEscalations = escalations.Count(e => e.Status != "resolved");
                var totalEscalations = escalations.Count;

                // Determine performance rating
                var performanceRating = DeterminePerformanceRating(approvalRate, avgTurnaroundHours);

                // Get or create metrics record
                var metrics = await _context.ReviewerPerformanceMetrics
                    .FirstOrDefaultAsync(m => m.ReviewerId == reviewerId);

                if (metrics == null)
                {
                    metrics = new ReviewerPerformanceMetrics
                    {
                        ReviewerId = reviewerId
                    };
                    _context.ReviewerPerformanceMetrics.Add(metrics);
                }

                // Update metrics
                metrics.TotalAssignments = totalAssignments;
                metrics.ActiveAssignments = activeAssignments;
                metrics.CompletedAssignments = completedAssignments.Count;
                metrics.DocumentsApproved = approved;
                metrics.DocumentsRejected = rejected;
                metrics.DocumentsRevisionRequested = revisionRequested;
                metrics.AverageTurnaroundTimeHours = avgTurnaroundHours;
                metrics.ApprovalRatePercentage = approvalRate;
                metrics.RejectionRatePercentage = rejectionRate;
                metrics.RevisionRequestRatePercentage = revisionRate;
                metrics.EscalatedAssignments = activeEscalations;
                metrics.TotalEscalationCount = totalEscalations;
                metrics.LastCalculatedAt = DateTime.UtcNow;
                metrics.PerformanceRating = performanceRating;
                metrics.TotalHoursSpent = totalHours;
                metrics.AverageHoursPerAssignment = completedAssignments.Any() 
                    ? totalHours / completedAssignments.Count 
                    : 0.0;

                await _context.SaveChangesAsync();

                // Invalidate relevant caches after recalculation
                InvalidatePerformanceCache();

                _logger.LogInformation($"Metrics calculated for reviewer {reviewerId}");
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error calculating metrics for reviewer {reviewerId}: {ex.Message}");
            }
        }

        /// <summary>
        /// Recalculate metrics for all reviewers
        /// </summary>
        public async Task RecalculateAllMetricsAsync()
        {
            try
            {
                var reviewers = await _context.Users
                    .Where(u => u.IsActive && (u.Role == "ADMIN" || u.Role == "LEAD_REVIEWER"))
                    .Select(u => u.Id)
                    .ToListAsync();

                foreach (var reviewerId in reviewers)
                {
                    await RecalculateMetricsAsync(reviewerId);
                }

                // Invalidate all caches after recalculation
                InvalidatePerformanceCache();

                _logger.LogInformation($"Recalculated metrics for {reviewers.Count} reviewers");
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error recalculating all metrics: {ex.Message}");
            }
        }

        /// <summary>
        /// Get average turnaround time for a reviewer over the last N days
        /// </summary>
        public async Task<double> GetAverageTurnaroundTimeAsync(Guid reviewerId, int days = 30)
        {
            var startDate = DateTime.UtcNow.AddDays(-days);

            var completedAssignments = await _context.ApplicationAssignments
                .Where(a => a.ReviewerId == reviewerId && 
                       !a.IsActive &&
                       a.Application != null &&
                       a.Application.CompletedAt.HasValue &&
                       a.Application.CompletedAt >= startDate)
                .Include(a => a.Application)
                .ToListAsync();

            if (!completedAssignments.Any())
                return 0.0;

            var totalHours = completedAssignments.Sum(a => (a.Application!.CompletedAt!.Value - a.AssignedAt).TotalHours);
            return totalHours / completedAssignments.Count;
        }

        /// <summary>
        /// Get top performing reviewers
        /// </summary>
        public async Task<List<ReviewerPerformanceMetricsDto>> GetTopPerformersAsync(int count = 5)
        {
            var cacheKey = TOP_PERFORMERS_CACHE_KEY_PREFIX + count;
            if (_memoryCache.TryGetValue(cacheKey, out List<ReviewerPerformanceMetricsDto> cachedMetrics))
            {
                return cachedMetrics;
            }

            var metrics = await _context.ReviewerPerformanceMetrics
                .AsNoTracking()
                .Include(m => m.Reviewer)
                .Where(m => m.Reviewer != null && m.Reviewer.IsActive && m.CompletedAssignments > 0)
                .OrderByDescending(m => m.ApprovalRatePercentage)
                .ThenBy(m => m.AverageTurnaroundTimeHours)
                .Take(count)
                .ToListAsync();

            var result = metrics.Select(MapToDto).ToList();

            var cacheOptions = new MemoryCacheEntryOptions()
                .SetSize(result.Count)
                .SetAbsoluteExpiration(TimeSpan.FromMinutes(CACHE_DURATION_MINUTES));
            _memoryCache.Set(cacheKey, result, cacheOptions);

            return result;
        }

        /// <summary>
        /// Get bottom performing reviewers
        /// </summary>
        public async Task<List<ReviewerPerformanceMetricsDto>> GetBottomPerformersAsync(int count = 5)
        {
            var cacheKey = BOTTOM_PERFORMERS_CACHE_KEY_PREFIX + count;
            if (_memoryCache.TryGetValue(cacheKey, out List<ReviewerPerformanceMetricsDto> cachedMetrics))
            {
                return cachedMetrics;
            }

            var metrics = await _context.ReviewerPerformanceMetrics
                .AsNoTracking()
                .Include(m => m.Reviewer)
                .Where(m => m.Reviewer != null && m.Reviewer.IsActive && m.CompletedAssignments > 0)
                .OrderBy(m => m.ApprovalRatePercentage)
                .ThenByDescending(m => m.AverageTurnaroundTimeHours)
                .Take(count)
                .ToListAsync();

            var result = metrics.Select(MapToDto).ToList();

            var cacheOptions = new MemoryCacheEntryOptions()
                .SetSize(result.Count)
                .SetAbsoluteExpiration(TimeSpan.FromMinutes(CACHE_DURATION_MINUTES));
            _memoryCache.Set(cacheKey, result, cacheOptions);

            return result;
        }

        private void InvalidatePerformanceCache()
        {
            _memoryCache.Remove(ALL_METRICS_CACHE_KEY);
            _memoryCache.Remove(WORKLOAD_DISTRIBUTION_CACHE_KEY);

            // Remove top/bottom performers cache for common count values
            for (int i = 1; i <= 10; i++)
            {
                _memoryCache.Remove(TOP_PERFORMERS_CACHE_KEY_PREFIX + i);
                _memoryCache.Remove(BOTTOM_PERFORMERS_CACHE_KEY_PREFIX + i);
            }
        }

        private string DeterminePerformanceRating(double approvalRate, double avgTurnaroundHours)
        {
            // Rating based on approval rate and turnaround time
            if (approvalRate >= 90 && avgTurnaroundHours <= 24)
                return "Excellent";
            else if (approvalRate >= 75 && avgTurnaroundHours <= 48)
                return "Good";
            else if (approvalRate >= 50 && avgTurnaroundHours <= 72)
                return "Average";
            else
                return "Poor";
        }

        /// <summary>
        /// Get workload distribution across all reviewers
        /// </summary>
        public async Task<WorkloadDistributionSummaryDto> GetWorkloadDistributionAsync()
        {
            if (_memoryCache.TryGetValue(WORKLOAD_DISTRIBUTION_CACHE_KEY, out WorkloadDistributionSummaryDto cachedResult))
            {
                return cachedResult;
            }

            var metrics = await _context.ReviewerPerformanceMetrics
                .AsNoTracking()
                .Include(m => m.Reviewer)
                .Where(m => m.Reviewer!.IsActive)
                .OrderBy(m => m.Reviewer!.FirstName)
                .ToListAsync();

            if (!metrics.Any())
            {
                var emptyResult = new WorkloadDistributionSummaryDto
                {
                    TotalReviewers = 0,
                    TotalAssignments = 0,
                    AverageAssignmentsPerReviewer = 0,
                    MaxAssignments = 0,
                    MinAssignments = 0,
                    OverallApprovalRate = 0,
                    ReviewerWorkloads = new List<WorkloadDistributionDto>()
                };

                var cacheOptions = new MemoryCacheEntryOptions()
                    .SetSize(1)
                    .SetAbsoluteExpiration(TimeSpan.FromMinutes(CACHE_DURATION_MINUTES));
                _memoryCache.Set(WORKLOAD_DISTRIBUTION_CACHE_KEY, emptyResult, cacheOptions);

                return emptyResult;
            }

            var workloads = await MapToWorkloadDtosAsync(metrics);
            var totalAssignments = workloads.Sum(w => w.TotalAssignments);
            var totalApprovalRate = workloads.Any() ? workloads.Average(w => w.ApprovalRatePercentage) : 0;
            var totalHours = workloads.Sum(w => w.TotalHoursSpent);
            var avgEscalationRate = workloads.Any() ? workloads.Average(w => w.EscalationRatePercentage) : 0;
            var avgTurnaroundTime = workloads.Any() ? workloads.Average(w => w.AverageTurnaroundTimeHours) : 0;

            // Determine overall team health
            var overallHealth = totalApprovalRate >= 85 && avgEscalationRate < 10 ? "Excellent" :
                               totalApprovalRate >= 75 && avgEscalationRate < 15 ? "Good" :
                               totalApprovalRate >= 60 && avgEscalationRate < 20 ? "Fair" : "Poor";

            var reviewersAtRisk = workloads.Count(w => w.BurnoutRisk == "High");
            var reviewersOverCapacity = workloads.Count(w => w.CapacityUtilizationPercentage > 100);

            var result = new WorkloadDistributionSummaryDto
            {
                TotalReviewers = workloads.Count,
                TotalAssignments = totalAssignments,
                AverageAssignmentsPerReviewer = workloads.Any() ? totalAssignments / workloads.Count : 0,
                MaxAssignments = workloads.Any() ? workloads.Max(w => w.TotalAssignments) : 0,
                MinAssignments = workloads.Any() ? workloads.Min(w => w.TotalAssignments) : 0,
                OverallApprovalRate = Math.Round(totalApprovalRate, 2),
                AverageEscalationRate = Math.Round(avgEscalationRate, 2),
                TeamAverageTurnaroundTime = Math.Round(avgTurnaroundTime, 2),
                OverallTeamHealth = overallHealth,
                TotalTeamHoursSpent = Math.Round(totalHours, 2),
                AverageHoursPerReviewer = Math.Round(totalHours / Math.Max(1, workloads.Count), 2),
                TeamCapacityUtilization = Math.Round((totalHours / (160 * workloads.Count)) * 100, 2),
                ReviewersAtRisk = reviewersAtRisk,
                ReviewersOverCapacity = reviewersOverCapacity,
                TeamWeeklyVelocity = Math.Round(workloads.Average(w => w.WeeklyWorkloadAverage), 2),
                TeamWorkloadTrend = DetermineTeamTrend(workloads),
                VelocityChangePercentage = Math.Round(workloads.Average(w => w.VelocityChangePercentage), 2),
                HighRiskReviewers = reviewersAtRisk,
                ReviewerWorkloads = workloads
            };

            var cacheOpts = new MemoryCacheEntryOptions()
                .SetSize(workloads.Count)
                .SetAbsoluteExpiration(TimeSpan.FromMinutes(CACHE_DURATION_MINUTES));
            _memoryCache.Set(WORKLOAD_DISTRIBUTION_CACHE_KEY, result, cacheOpts);

            return result;
        }

        /// <summary>
        /// Get workload distribution with pagination and sorting
        /// </summary>
        public async Task<PaginatedWorkloadDistributionDto> GetWorkloadDistributionPagedAsync(
            int pageNumber = 1,
            int pageSize = 25,
            string sortBy = "ReviewerName",
            bool sortDescending = false)
        {
            pageNumber = Math.Max(1, pageNumber);
            pageSize = Math.Max(1, Math.Min(pageSize, 100)); // Cap at 100

            var metricsQuery = _context.ReviewerPerformanceMetrics
                .AsNoTracking()
                .Include(m => m.Reviewer)
                .Where(m => m.Reviewer != null && m.Reviewer.IsActive && m.ActiveAssignments > 0);

            // Load all metrics once
            var allMetrics = await metricsQuery.ToListAsync();

            // Apply sorting in memory
            var sortedMetrics = sortBy.ToLower() switch
            {
                "reviewername" => sortDescending
                    ? allMetrics.OrderByDescending(m => m.Reviewer!.FirstName).ThenByDescending(m => m.Reviewer!.LastName).ToList()
                    : allMetrics.OrderBy(m => m.Reviewer!.FirstName).ThenBy(m => m.Reviewer!.LastName).ToList(),

                "workload" or "totalassignments" => sortDescending
                    ? allMetrics.OrderByDescending(m => m.TotalAssignments).ToList()
                    : allMetrics.OrderBy(m => m.TotalAssignments).ToList(),

                "activeassignments" => sortDescending
                    ? allMetrics.OrderByDescending(m => m.ActiveAssignments).ToList()
                    : allMetrics.OrderBy(m => m.ActiveAssignments).ToList(),

                "approvalrate" => sortDescending
                    ? allMetrics.OrderByDescending(m => m.ApprovalRatePercentage).ToList()
                    : allMetrics.OrderBy(m => m.ApprovalRatePercentage).ToList(),

                "turnaroundtime" => sortDescending
                    ? allMetrics.OrderByDescending(m => m.AverageTurnaroundTimeHours).ToList()
                    : allMetrics.OrderBy(m => m.AverageTurnaroundTimeHours).ToList(),

                "performancerating" => sortDescending
                    ? allMetrics.OrderByDescending(m => m.PerformanceRating).ToList()
                    : allMetrics.OrderBy(m => m.PerformanceRating).ToList(),

                "hours" or "totalhoursspent" => sortDescending
                    ? allMetrics.OrderByDescending(m => m.TotalHoursSpent).ToList()
                    : allMetrics.OrderBy(m => m.TotalHoursSpent).ToList(),

                "capacityutilization" => sortDescending
                    ? allMetrics.OrderByDescending(m => (m.TotalHoursSpent / 160) * 100).ToList()
                    : allMetrics.OrderBy(m => (m.TotalHoursSpent / 160) * 100).ToList(),

                _ => allMetrics.OrderBy(m => m.Reviewer!.FirstName).ThenBy(m => m.Reviewer!.LastName).ToList()
            };

            var totalCount = sortedMetrics.Count;
            var totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);

            // Get the page
            var pageMetrics = sortedMetrics
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToList();

            var pageWorkloads = await MapToWorkloadDtosAsync(pageMetrics);

            // Calculate overall summary from all metrics
            var allWorkloads = await MapToWorkloadDtosAsync(sortedMetrics);
            var totalAssignments = allWorkloads.Sum(w => w.TotalAssignments);
            var totalApprovalRate = allWorkloads.Any() ? allWorkloads.Average(w => w.ApprovalRatePercentage) : 0;
            var totalHours = allWorkloads.Sum(w => w.TotalHoursSpent);
            var avgEscalationRate = allWorkloads.Any() ? allWorkloads.Average(w => w.EscalationRatePercentage) : 0;
            var avgTurnaroundTime = allWorkloads.Any() ? allWorkloads.Average(w => w.AverageTurnaroundTimeHours) : 0;
            var reviewersAtRisk = allWorkloads.Count(w => w.BurnoutRisk == "High");
            var reviewersOverCapacity = allWorkloads.Count(w => w.CapacityUtilizationPercentage > 100);

            var overallHealth = totalApprovalRate >= 85 && avgEscalationRate < 10 ? "Excellent" :
                               totalApprovalRate >= 75 && avgEscalationRate < 15 ? "Good" :
                               totalApprovalRate >= 60 && avgEscalationRate < 20 ? "Fair" : "Poor";

            var summary = new WorkloadDistributionSummaryDto
            {
                TotalReviewers = allWorkloads.Count,
                TotalAssignments = totalAssignments,
                AverageAssignmentsPerReviewer = allWorkloads.Any() ? totalAssignments / allWorkloads.Count : 0,
                MaxAssignments = allWorkloads.Any() ? allWorkloads.Max(w => w.TotalAssignments) : 0,
                MinAssignments = allWorkloads.Any() ? allWorkloads.Min(w => w.TotalAssignments) : 0,
                OverallApprovalRate = Math.Round(totalApprovalRate, 2),
                AverageEscalationRate = Math.Round(avgEscalationRate, 2),
                TeamAverageTurnaroundTime = Math.Round(avgTurnaroundTime, 2),
                OverallTeamHealth = overallHealth,
                TotalTeamHoursSpent = Math.Round(totalHours, 2),
                AverageHoursPerReviewer = Math.Round(totalHours / Math.Max(1, allWorkloads.Count), 2),
                TeamCapacityUtilization = Math.Round((totalHours / (160 * allWorkloads.Count)) * 100, 2),
                ReviewersAtRisk = reviewersAtRisk,
                ReviewersOverCapacity = reviewersOverCapacity,
                TeamWeeklyVelocity = Math.Round(allWorkloads.Average(w => w.WeeklyWorkloadAverage), 2),
                TeamWorkloadTrend = DetermineTeamTrend(allWorkloads),
                VelocityChangePercentage = Math.Round(allWorkloads.Average(w => w.VelocityChangePercentage), 2),
                HighRiskReviewers = reviewersAtRisk,
                ReviewerWorkloads = new List<WorkloadDistributionDto>()
            };

            return new PaginatedWorkloadDistributionDto
            {
                PageNumber = pageNumber,
                PageSize = pageSize,
                TotalCount = totalCount,
                TotalPages = totalPages,
                HasPreviousPage = pageNumber > 1,
                HasNextPage = pageNumber < totalPages,
                Summary = summary,
                ReviewerWorkloads = pageWorkloads
            };
        }

        /// <summary>
        /// Determine overall team trend based on workload changes
        /// </summary>
        private string DetermineTeamTrend(List<WorkloadDistributionDto> workloads)
        {
            var avgVelocityChange = workloads.Any() ? workloads.Average(w => w.VelocityChangePercentage) : 0;
            return avgVelocityChange > 5 ? "Increasing" :
                   avgVelocityChange < -5 ? "Decreasing" : "Stable";
        }

        private async Task<List<WorkloadDistributionDto>> MapToWorkloadDtosAsync(List<ReviewerPerformanceMetrics> metrics)
        {
            var totalAssignments = metrics.Sum(m => m.TotalAssignments);
            var maxCapacityHours = 160.0; // Standard monthly capacity (40 hrs/week * 4 weeks)

            var now = DateTime.UtcNow;
            var oneWeekAgo = now.AddDays(-7);
            var twoWeeksAgo = now.AddDays(-14);

            // OPTIMIZATION: Batch load all assignments for all reviewers in ONE query
            // Using a simpler query pattern to avoid WITH clause issues
            var allAssignments = await _context.ApplicationAssignments
                .AsNoTracking()
                .Include(a => a.Application)
                .ToListAsync();

            // Group assignments by reviewer for fast lookup
            var assignmentsByReviewer = allAssignments
                .GroupBy(a => a.ReviewerId)
                .ToDictionary(g => g.Key, g => g.ToList());

            var result = new List<WorkloadDistributionDto>();

            foreach (var m in metrics)
            {
                // Calculate quality metrics
                var totalDocumentsReviewed = m.DocumentsApproved + m.DocumentsRejected + m.DocumentsRevisionRequested;
                var escalationRate = m.TotalAssignments > 0 ? (m.EscalatedAssignments * 100.0 / m.TotalAssignments) : 0;
                var revisionRate = totalDocumentsReviewed > 0 ? (m.DocumentsRevisionRequested * 100.0 / totalDocumentsReviewed) : 0;

                // Estimate rework percentage (applications that had rework)
                var reworkPercentage = m.TotalAssignments > 0 
                    ? Math.Min(100, escalationRate + revisionRate / 2) 
                    : 0;

                // Calculate capacity metrics
                var capacityUtilization = (m.TotalHoursSpent / maxCapacityHours) * 100;
                var estimatedHoursRemaining = m.ActiveAssignments > 0 
                    ? m.AverageHoursPerAssignment * m.ActiveAssignments 
                    : 0;

                var estimatedCompletionDate = estimatedHoursRemaining > 0
                    ? DateTime.UtcNow.AddHours(estimatedHoursRemaining)
                    : (DateTime?)null;

                // Determine capacity status based on utilization
                var capacityStatus = capacityUtilization > 100 ? "Red" :
                                    capacityUtilization > 85 ? "Yellow" : "Green";

                // Calculate burnout risk
                var burnoutRisk = capacityUtilization > 100 ? "High" :
                                 capacityUtilization > 80 ? "Medium" : "Low";

                // OPTIMIZATION: Calculate SLA from pre-loaded in-memory data instead of database query
                var reviewerAssignments = assignmentsByReviewer.TryGetValue(m.ReviewerId, out var assignments) 
                    ? assignments 
                    : new List<ApplicationAssignment>();

                var slaCompliance = CalculateSLAComplianceInMemory(reviewerAssignments);

                // OPTIMIZATION: Calculate trend from pre-loaded in-memory data instead of database query
                var (weeksAvgHours, lastWeekHours, prevWeekHours) = CalculateWeeklyHoursTrendInMemory(
                    reviewerAssignments, 
                    now, oneWeekAgo, twoWeeksAgo);

                var velocityChange = prevWeekHours > 0 ? ((lastWeekHours - prevWeekHours) / prevWeekHours) * 100 : 0;

                var avgDocsPerApp = m.TotalAssignments > 0 ? (double)totalDocumentsReviewed / m.TotalAssignments : 0;

                var workloadTrend = velocityChange > 5 ? "Increasing" :
                                   velocityChange < -5 ? "Decreasing" : "Stable";

                result.Add(new WorkloadDistributionDto
                {
                    // Basic Info
                    ReviewerId = m.ReviewerId,
                    ReviewerName = m.Reviewer != null ? $"{m.Reviewer.FirstName} {m.Reviewer.LastName}" : "Unknown",
                    ReviewerEmail = m.Reviewer?.Email ?? string.Empty,

                    // Workload Breakdown
                    ActiveAssignments = m.ActiveAssignments,
                    PendingAssignments = m.TotalAssignments - m.CompletedAssignments - m.ActiveAssignments,
                    CompletedAssignments = m.CompletedAssignments,
                    TotalAssignments = m.TotalAssignments,
                    WorkloadPercentage = totalAssignments > 0 ? Math.Round((m.TotalAssignments * 100.0 / totalAssignments), 2) : 0,

                    // Performance Metrics
                    ApprovalRatePercentage = Math.Round(m.ApprovalRatePercentage, 2),
                    RejectionRatePercentage = Math.Round(m.RejectionRatePercentage, 2),
                    RevisionRequestRatePercentage = Math.Round(m.RevisionRequestRatePercentage, 2),
                    AverageTurnaroundTimeHours = Math.Round(m.AverageTurnaroundTimeHours, 2),
                    PerformanceRating = m.PerformanceRating,

                    // Quality & Escalation
                    EscalatedAssignments = m.EscalatedAssignments,
                    EscalationRatePercentage = Math.Round(escalationRate, 2),
                    RevisionRequestRate = Math.Round(revisionRate, 2),
                    ReworkPercentage = Math.Round(reworkPercentage, 2),
                    DocumentsPending = 0,
                    DocumentsApproved = m.DocumentsApproved,
                    DocumentsRejected = m.DocumentsRejected,
                    DocumentsRevisionRequested = m.DocumentsRevisionRequested,
                    AverageDocumentsPerApplication = Math.Round(avgDocsPerApp, 2),

                    // Hours Tracking
                    TotalHoursSpent = Math.Round(m.TotalHoursSpent, 2),
                    AverageHoursPerAssignment = Math.Round(m.AverageHoursPerAssignment, 2),
                    EstimatedHoursRemaining = Math.Round(estimatedHoursRemaining, 2),
                    EstimatedCompletionDate = estimatedCompletionDate,

                    // Capacity & Health
                    MaxCapacityHours = maxCapacityHours,
                    CapacityUtilizationPercentage = Math.Round(capacityUtilization, 2),
                    AvailableCapacityHours = Math.Round(Math.Max(0, maxCapacityHours - m.TotalHoursSpent), 2),
                    CapacityStatus = capacityStatus,
                    BurnoutRisk = burnoutRisk,
                    SLAComplianceRate = Math.Round(slaCompliance, 2),

                    // Trend & Velocity
                    WeeklyWorkloadAverage = Math.Round(weeksAvgHours, 2),
                    WorkloadTrend = workloadTrend,
                    AverageHoursLastWeek = Math.Round(lastWeekHours, 2),
                    AverageHoursPreviousWeek = Math.Round(prevWeekHours, 2),
                    VelocityChangePercentage = Math.Round(velocityChange, 2),
                    LastMetricsUpdateTime = m.LastCalculatedAt
                });
            }

            return result;
        }

        /// <summary>
        /// OPTIMIZED: Calculate SLA compliance from pre-loaded in-memory data instead of database query
        /// This replaces CalculateSLAComplianceAsync for performance
        /// </summary>
        private double CalculateSLAComplianceInMemory(List<ApplicationAssignment> assignments)
        {
            const int slaDaysTarget = 2; // 48-hour SLA

            var completedAssignments = assignments
                .Where(a => !a.IsActive && a.Application != null && a.Application.CompletedAt.HasValue)
                .ToList();

            if (!completedAssignments.Any())
                return 100.0; // No completed assignments = perfect compliance

            var compliantCount = completedAssignments.Count(a =>
                (a.Application.CompletedAt.Value - a.AssignedAt).TotalDays <= slaDaysTarget);

            return (compliantCount * 100.0 / completedAssignments.Count);
        }

        /// <summary>
        /// OPTIMIZED: Calculate weekly trends from pre-loaded in-memory data instead of database queries
        /// This replaces CalculateWeeklyHoursTrendAsync for performance
        /// </summary>
        private (double weeksAvgHours, double lastWeekHours, double prevWeekHours) CalculateWeeklyHoursTrendInMemory(
            List<ApplicationAssignment> assignments, 
            DateTime now, 
            DateTime oneWeekAgo, 
            DateTime twoWeeksAgo)
        {
            var lastWeekAssignments = assignments
                .Where(a => a.AssignedAt >= oneWeekAgo)
                .ToList();

            var prevWeekAssignments = assignments
                .Where(a => a.AssignedAt >= twoWeeksAgo && a.AssignedAt < oneWeekAgo)
                .ToList();

            // Calculate hours for each period
            var lastWeekHours = CalculateTotalHours(lastWeekAssignments, now);
            var prevWeekHours = CalculateTotalHours(prevWeekAssignments, now);
            var averageOfTwoWeeks = (lastWeekHours + prevWeekHours) / 2.0;

            return (averageOfTwoWeeks, lastWeekHours, prevWeekHours);
        }

        /// <summary>
        /// Helper: Calculate total hours from a list of assignments
        /// </summary>
        private double CalculateTotalHours(List<ApplicationAssignment> assignments, DateTime now)
        {
            return assignments.Sum(a =>
            {
                var hours = a.Application?.CompletedAt.HasValue == true && a.Application.CompletedAt > a.AssignedAt
                    ? (a.Application.CompletedAt.Value - a.AssignedAt).TotalHours
                    : (now - a.AssignedAt).TotalHours;
                return hours;
            });
        }

        // DEPRECATED: Old methods replaced by optimized in-memory versions above

        private ReviewerPerformanceMetricsDto MapToDto(ReviewerPerformanceMetrics metrics)
        {
            return new ReviewerPerformanceMetricsDto
            {
                Id = metrics.Id,
                ReviewerId = metrics.ReviewerId,
                ReviewerName = metrics.Reviewer != null ? $"{metrics.Reviewer.FirstName} {metrics.Reviewer.LastName}" : "Unknown",
                TotalAssignments = metrics.TotalAssignments,
                ActiveAssignments = metrics.ActiveAssignments,
                CompletedAssignments = metrics.CompletedAssignments,
                DocumentsApproved = metrics.DocumentsApproved,
                DocumentsRejected = metrics.DocumentsRejected,
                DocumentsRevisionRequested = metrics.DocumentsRevisionRequested,
                AverageTurnaroundTimeHours = Math.Round(metrics.AverageTurnaroundTimeHours, 2),
                ApprovalRatePercentage = Math.Round(metrics.ApprovalRatePercentage, 2),
                RejectionRatePercentage = Math.Round(metrics.RejectionRatePercentage, 2),
                RevisionRequestRatePercentage = Math.Round(metrics.RevisionRequestRatePercentage, 2),
                EscalatedAssignments = metrics.EscalatedAssignments,
                TotalEscalationCount = metrics.TotalEscalationCount,
                LastCalculatedAt = metrics.LastCalculatedAt,
                PerformanceRating = metrics.PerformanceRating,
                AverageHoursPerAssignment = Math.Round(metrics.AverageHoursPerAssignment, 2)
            };
        }

        /// <summary>
        /// Get reassignment history for the specified time period
        /// </summary>
        public async Task<List<ReassignmentHistoryDto>> GetReassignmentHistoryAsync(int days = 30, int pageSize = 100)
        {
            var startDate = DateTime.UtcNow.AddDays(-days);

            var history = await _context.ReassignmentHistories
                .AsNoTracking()
                .Where(h => h.ReassignedAt >= startDate)
                .Include(h => h.PreviousReviewer)
                .Include(h => h.NewReviewer)
                .Include(h => h.ReassignedByUser)
                .Include(h => h.ApplicationAssignment)
                .Include(h => h.ApplicationAssignment.Application)
                .OrderByDescending(h => h.ReassignedAt)
                .Take(pageSize)
                .ToListAsync();

            return history.Select(h => new ReassignmentHistoryDto
            {
                Id = h.Id,
                ApplicationId = h.ApplicationId,
                ApplicationRefNumber = h.ApplicationAssignment?.Application?.ApplicationNumber ?? "N/A",
                ApplicationAssignmentId = h.ApplicationAssignmentId,
                PreviousReviewerId = h.PreviousReviewerId,
                PreviousReviewerName = h.PreviousReviewer?.FirstName + " " + h.PreviousReviewer?.LastName,
                NewReviewerId = h.NewReviewerId,
                NewReviewerName = h.NewReviewer?.FirstName + " " + h.NewReviewer?.LastName,
                ReassignmentReason = h.ReassignmentReason,
                ReassignmentDetails = h.ReassignmentDetails,
                ReassignedByUserId = h.ReassignedByUserId,
                ReassignedByUserName = h.ReassignedByUser != null ? (h.ReassignedByUser.FirstName + " " + h.ReassignedByUser.LastName) : "System",
                ReassignedAt = h.ReassignedAt,
                PreviousAssignmentStatus = h.PreviousAssignmentStatus,
                NewAssignmentStatus = h.NewAssignmentStatus,
                IsCompleted = h.IsCompleted
            }).ToList();
        }

        /// <summary>
        /// Get reassignment history for a specific reviewer
        /// </summary>
        public async Task<List<ReassignmentHistoryDto>> GetReassignmentHistoryForReviewerAsync(Guid reviewerId, int days = 30)
        {
            var startDate = DateTime.UtcNow.AddDays(-days);

            var history = await _context.ReassignmentHistories
                .AsNoTracking()
                .Where(h => (h.PreviousReviewerId == reviewerId || h.NewReviewerId == reviewerId) && h.ReassignedAt >= startDate)
                .Include(h => h.PreviousReviewer)
                .Include(h => h.NewReviewer)
                .Include(h => h.ReassignedByUser)
                .Include(h => h.ApplicationAssignment)
                .Include(h => h.ApplicationAssignment.Application)
                .OrderByDescending(h => h.ReassignedAt)
                .ToListAsync();

            return history.Select(h => new ReassignmentHistoryDto
            {
                Id = h.Id,
                ApplicationId = h.ApplicationId,
                ApplicationRefNumber = h.ApplicationAssignment?.Application?.ApplicationNumber ?? "N/A",
                ApplicationAssignmentId = h.ApplicationAssignmentId,
                PreviousReviewerId = h.PreviousReviewerId,
                PreviousReviewerName = h.PreviousReviewer?.FirstName + " " + h.PreviousReviewer?.LastName,
                NewReviewerId = h.NewReviewerId,
                NewReviewerName = h.NewReviewer?.FirstName + " " + h.NewReviewer?.LastName,
                ReassignmentReason = h.ReassignmentReason,
                ReassignmentDetails = h.ReassignmentDetails,
                ReassignedByUserId = h.ReassignedByUserId,
                ReassignedByUserName = h.ReassignedByUser != null ? (h.ReassignedByUser.FirstName + " " + h.ReassignedByUser.LastName) : "System",
                ReassignedAt = h.ReassignedAt,
                PreviousAssignmentStatus = h.PreviousAssignmentStatus,
                NewAssignmentStatus = h.NewAssignmentStatus,
                IsCompleted = h.IsCompleted
            }).ToList();
        }

        /// <summary>
        /// Get reassignment audit metrics and statistics
        /// </summary>
        public async Task<ReassignmentAuditMetricsDto> GetReassignmentMetricsAsync()
        {
            var allReassignments = await _context.ReassignmentHistories
                .AsNoTracking()
                .Include(h => h.PreviousReviewer)
                .Include(h => h.NewReviewer)
                .Include(h => h.ReassignedByUser)
                .ToListAsync();

            var metrics = new ReassignmentAuditMetricsDto
            {
                TotalReassignments = allReassignments.Count,
                ManualReassignments = allReassignments.Count(h => h.ReassignedByUserId != null),
                AutomaticReassignments = allReassignments.Count(h => h.ReassignedByUserId == null),
                ReassignmentsFromUnavailability = allReassignments.Count(h => h.ReassignmentReason == "AvailabilityChange"),
                WorkloadBalancingReassignments = allReassignments.Count(h => h.ReassignmentReason == "CriteriaBased" || h.ReassignmentReason == "AutoBalance"),
                PerformanceIssueReassignments = allReassignments.Count(h => h.ReassignmentReason == "PerformanceIssue"),
                ReassignmentsByReason = allReassignments
                    .GroupBy(h => h.ReassignmentReason)
                    .ToDictionary(g => g.Key, g => g.Count()),
                RecentReassignments = allReassignments
                    .OrderByDescending(h => h.ReassignedAt)
                    .Take(10)
                    .Select(h => new ReassignmentHistoryDto
                    {
                        Id = h.Id,
                        ApplicationId = h.ApplicationId,
                        PreviousReviewerId = h.PreviousReviewerId,
                        PreviousReviewerName = h.PreviousReviewer?.FirstName + " " + h.PreviousReviewer?.LastName,
                        NewReviewerId = h.NewReviewerId,
                        NewReviewerName = h.NewReviewer?.FirstName + " " + h.NewReviewer?.LastName,
                        ReassignmentReason = h.ReassignmentReason,
                        ReassignedAt = h.ReassignedAt
                    }).ToList()
            };

            // Count reassignments by reviewer (as source)
            metrics.ReassignmentCountByReviewer = allReassignments
                .GroupBy(h => h.PreviousReviewerId)
                .ToDictionary(g => g.Key, g => g.Count());

            if (metrics.TotalReassignments > 0 && metrics.ReassignmentCountByReviewer.Count > 0)
            {
                metrics.AverageReassignmentsPerReviewer = (double)metrics.TotalReassignments / metrics.ReassignmentCountByReviewer.Count;
            }

            return metrics;
        }

        /// <summary>
        /// Get hours spent on a specific application by all reviewers.
        /// Used for audit trail and application-level reporting.
        /// </summary>
        public async Task<List<ApplicationHoursTrackingDto>> GetApplicationHoursTrackingAsync(Guid applicationId)
        {
            var application = await _context.Applications
                .AsNoTracking()
                .FirstOrDefaultAsync(a => a.Id == applicationId);

            if (application == null)
                return new List<ApplicationHoursTrackingDto>();

            var assignments = await _context.ApplicationAssignments
                .AsNoTracking()
                .Where(a => a.ApplicationId == applicationId)
                .Include(a => a.Reviewer)
                .Include(a => a.Application)
                .ToListAsync();

            // Get all documents for this application with reviewer info
            var allDocuments = await _context.ApplicationDocuments
                .AsNoTracking()
                .Where(d => d.ApplicationId == applicationId)
                .ToListAsync();

            var result = assignments.Select(a => 
            {
                // Filter documents reviewed by THIS specific reviewer only
                var reviewedByThisReviewer = allDocuments
                    .Where(d => d.ReviewedBy == a.ReviewerId)
                    .ToList();

                // Calculate hours only for active assignment period
                var hoursSpent = 0.0;
                if (application.CompletedAt.HasValue && application.CompletedAt > a.AssignedAt)
                {
                    hoursSpent = (application.CompletedAt.Value - a.AssignedAt).TotalHours;
                }
                else
                {
                    // If not completed, use current time but only if assignment is still active
                    if (a.IsActive)
                    {
                        hoursSpent = (DateTime.UtcNow - a.AssignedAt).TotalHours;
                    }
                }

                return new ApplicationHoursTrackingDto
                {
                    ApplicationId = applicationId,
                    ApplicationNumber = application.ApplicationNumber,
                    ApplicantName = $"{application.FirstName} {application.LastName}".Trim(),
                    AssignedAt = a.AssignedAt,
                    CompletedAt = application.CompletedAt,
                    ReviewerId = a.ReviewerId,
                    ReviewerName = a.Reviewer != null ? $"{a.Reviewer.FirstName} {a.Reviewer.LastName}".Trim() : "Unknown",
                    ReviewerEmail = a.Reviewer?.Email ?? string.Empty,
                    HoursSpent = hoursSpent,
                    DocumentsReviewed = reviewedByThisReviewer.Count,
                    DocumentsApproved = reviewedByThisReviewer.Count(d => d.ReviewStatus == "approved"),
                    DocumentsRejected = reviewedByThisReviewer.Count(d => d.ReviewStatus == "rejected"),
                    DocumentsRevisionRequested = reviewedByThisReviewer.Count(d => d.ReviewStatus == "revision_requested")
                };
            }).ToList();

            return result;
        }

        /// <summary>
        /// Get audit summary of hours spent by a specific reviewer over a date range.
        /// Used for compliance and billing purposes.
        /// </summary>
        public async Task<ReviewerHoursAuditSummaryDto> GetReviewerHoursAuditSummaryAsync(
            Guid reviewerId,
            DateTime? dateFrom = null,
            DateTime? dateTo = null)
        {
            var reviewer = await _context.Users
                .AsNoTracking()
                .FirstOrDefaultAsync(u => u.Id == reviewerId);

            if (reviewer == null)
                throw new InvalidOperationException($"Reviewer with ID '{reviewerId}' not found");

            var startDate = dateFrom ?? DateTime.UtcNow.AddMonths(-1);
            var endDate = dateTo ?? DateTime.UtcNow;

            // Get all assignments for this reviewer in the date range
            var assignments = await _context.ApplicationAssignments
                .AsNoTracking()
                .Where(a => a.ReviewerId == reviewerId && a.AssignedAt >= startDate && a.AssignedAt <= endDate)
                .Include(a => a.Application)
                .ToListAsync();

            var applicationIds = assignments.Select(a => a.ApplicationId).Distinct().ToList();

            // Get document reviews for these applications, filtered by THIS reviewer
            var documentReviews = await _context.ApplicationDocuments
                .AsNoTracking()
                .Where(d => applicationIds.Contains(d.ApplicationId) && d.ReviewedBy == reviewerId)
                .ToListAsync();

            // Calculate hours for each application assignment
            var applicationHours = new List<ApplicationHoursTrackingDto>();
            foreach (var assignment in assignments)
            {
                var app = assignment.Application;

                // Count documents reviewed by this reviewer for this specific application
                var docsReviewedByThisReviewer = documentReviews
                    .Where(d => d.ApplicationId == assignment.ApplicationId)
                    .ToList();

                // Calculate hours: from assignment to completion (or current time if still active)
                var hoursSpent = 0.0;
                if (app != null && app.CompletedAt.HasValue && app.CompletedAt > assignment.AssignedAt)
                {
                    hoursSpent = (app.CompletedAt.Value - assignment.AssignedAt).TotalHours;
                }
                else if (assignment.IsActive && app != null)
                {
                    // Only count current time if assignment is still active
                    hoursSpent = (DateTime.UtcNow - assignment.AssignedAt).TotalHours;
                }

                applicationHours.Add(new ApplicationHoursTrackingDto
                {
                    ApplicationId = assignment.ApplicationId,
                    ApplicationNumber = app?.ApplicationNumber ?? "Unknown",
                    ApplicantName = app != null ? $"{app.FirstName} {app.LastName}".Trim() : "Unknown",
                    AssignedAt = assignment.AssignedAt,
                    CompletedAt = app?.CompletedAt,
                    ReviewerId = reviewerId,
                    ReviewerName = $"{reviewer.FirstName} {reviewer.LastName}".Trim(),
                    ReviewerEmail = reviewer.Email,
                    HoursSpent = hoursSpent,
                    DocumentsReviewed = docsReviewedByThisReviewer.Count,
                    DocumentsApproved = docsReviewedByThisReviewer.Count(d => d.ReviewStatus == "approved"),
                    DocumentsRejected = docsReviewedByThisReviewer.Count(d => d.ReviewStatus == "rejected"),
                    DocumentsRevisionRequested = docsReviewedByThisReviewer.Count(d => d.ReviewStatus == "revision_requested")
                });
            }

            var totalHours = applicationHours.Sum(a => a.HoursSpent);
            var totalDocs = applicationHours.Sum(a => a.DocumentsReviewed);

            return new ReviewerHoursAuditSummaryDto
            {
                ReviewerId = reviewerId,
                ReviewerName = $"{reviewer.FirstName} {reviewer.LastName}".Trim(),
                ReviewerEmail = reviewer.Email,
                DateFrom = dateFrom,
                DateTo = dateTo,
                TotalHoursSpent = totalHours,
                TotalApplicationsProcessed = applicationHours.Count,
                TotalDocumentsReviewed = totalDocs,
                AverageHoursPerApplication = applicationHours.Count > 0 ? totalHours / applicationHours.Count : 0,
                ApplicationDetails = applicationHours
            };
        }
    }
}
