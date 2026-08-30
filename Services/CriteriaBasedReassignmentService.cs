using Microsoft.EntityFrameworkCore;
using MMGT.API.Data;
using MMGT.API.Models;
using MMGT.API.Services.Interfaces;

namespace MMGT.API.Services
{
    public class CriteriaBasedReassignmentService : ICriteriaBasedReassignmentService
    {
        private readonly ApplicationDbContext _context;
        private readonly IReviewerAvailabilityService _availabilityService;
        private readonly ILogger<CriteriaBasedReassignmentService> _logger;

        public CriteriaBasedReassignmentService(
            ApplicationDbContext context,
            IReviewerAvailabilityService availabilityService,
            ILogger<CriteriaBasedReassignmentService> logger)
        {
            _context = context;
            _availabilityService = availabilityService;
            _logger = logger;
        }

        public async Task<List<CriteriaReassignmentResultDto>> ExecuteReassignmentsByCriteriaAsync(ReassignmentCriteriaDto criteria)
        {
            try
            {
                var results = new List<CriteriaReassignmentResultDto>();

                // Get all active assignments
                var activeAssignments = await _context.ApplicationAssignments
                    .Where(a => a.IsActive)
                    .Include(a => a.Application)
                    .Include(a => a.Reviewer)
                    .ToListAsync();

                if (activeAssignments.Count == 0)
                {
                    _logger.LogInformation("No active assignments found for criteria-based reassignment");
                    return results;
                }

                // Calculate workload metrics
                var reviewerWorkloads = activeAssignments
                    .GroupBy(a => a.ReviewerId)
                    .Select(g => new
                    {
                        ReviewerId = g.Key,
                        Workload = g.Count(),
                        Assignments = g.ToList()
                    })
                    .ToList();

                double avgWorkload = reviewerWorkloads.Average(r => r.Workload);
                var workloadThreshold = avgWorkload * (criteria.WorkloadThresholdPercentage / 100);
                var targetMaxWorkload = avgWorkload * (criteria.TargetWorkloadMaxPercentage / 100);

                // Get performance metrics for prioritization
                var performanceMetrics = await _context.ReviewerPerformanceMetrics
                    .AsNoTracking()
                    .ToListAsync();

                // Process overloaded reviewers
                foreach (var overloaded in reviewerWorkloads.Where(r => r.Workload > workloadThreshold))
                {
                    var assignmentsToReassign = overloaded.Assignments
                        .OrderBy(a => a.AssignedAt)
                        .Take(criteria.MaxReassignments - results.Count)
                        .ToList();

                    foreach (var assignment in assignmentsToReassign)
                    {
                        if (results.Count >= criteria.MaxReassignments) break;

                        var targetReviewers = GetEligibleTargetReviewers(
                            reviewerWorkloads.Cast<dynamic>().ToList(),
                            overloaded.ReviewerId,
                            targetMaxWorkload,
                            criteria,
                            performanceMetrics);

                        if (!targetReviewers.Any())
                        {
                            results.Add(new CriteriaReassignmentResultDto
                            {
                                ApplicationId = assignment.ApplicationId,
                                ApplicationRefNumber = assignment.Application?.ApplicationNumber,
                                PreviousReviewerId = assignment.ReviewerId,
                                PreviousReviewerName = assignment.Reviewer?.FirstName + " " + assignment.Reviewer?.LastName,
                                Success = false,
                                ErrorMessage = "No eligible target reviewers found"
                            });
                            continue;
                        }

                        var targetReviewer = targetReviewers.First();
                        var reassignmentResult = await ExecuteReassignmentAsync(
                            assignment,
                            targetReviewer.ReviewerId,
                            "CriteriaBased",
                            $"Workload balancing: {overloaded.Workload} to {targetReviewer.Workload}");

                        results.Add(reassignmentResult);
                    }
                }

                _logger.LogInformation($"Completed criteria-based reassignments: {results.Count(r => r.Success)} successful, {results.Count(r => !r.Success)} failed");

                return results;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error executing criteria-based reassignments: {ex.Message}");
                throw;
            }
        }

        public async Task<List<CriteriaReassignmentResultDto>> GetReassignmentCandidatesByCriteriaAsync(Guid reviewerId, ReassignmentCriteriaDto criteria)
        {
            try
            {
                var results = new List<CriteriaReassignmentResultDto>();

                var reviewerAssignments = await _context.ApplicationAssignments
                    .Where(a => a.ReviewerId == reviewerId && a.IsActive)
                    .Include(a => a.Application)
                    .Include(a => a.Reviewer)
                    .ToListAsync();

                var allAssignments = await _context.ApplicationAssignments
                    .Where(a => a.IsActive)
                    .Include(a => a.Reviewer)
                    .ToListAsync();

                var reviewerWorkloads = allAssignments
                    .GroupBy(a => a.ReviewerId)
                    .Select(g => new { ReviewerId = g.Key, Workload = g.Count() })
                    .ToList();

                double avgWorkload = reviewerWorkloads.Average(r => r.Workload);
                var workloadThreshold = avgWorkload * (criteria.WorkloadThresholdPercentage / 100);

                var reviewerWorkload = reviewerWorkloads.FirstOrDefault(r => r.ReviewerId == reviewerId)?.Workload ?? 0;

                if (reviewerWorkload <= workloadThreshold)
                {
                    _logger.LogInformation($"Reviewer {reviewerId} workload ({reviewerWorkload}) is below threshold ({workloadThreshold})");
                    return results;
                }

                var performanceMetrics = await _context.ReviewerPerformanceMetrics.AsNoTracking().ToListAsync();
                var targetMaxWorkload = avgWorkload * (criteria.TargetWorkloadMaxPercentage / 100);

                var candidateList = reviewerWorkloads
                    .Select(r => new
                    {
                        r.ReviewerId,
                        r.Workload,
                        Performance = performanceMetrics.FirstOrDefault(p => p.ReviewerId == r.ReviewerId)?.ApprovalRatePercentage ?? 0
                    })
                    .Where(r => r.Workload < targetMaxWorkload && r.ReviewerId != reviewerId)
                    .OrderByDescending(r => r.Performance)
                    .ToList();

                foreach (var assignment in reviewerAssignments.OrderBy(a => a.AssignedAt).Take(criteria.MaxReassignments))
                {
                    var targetReviewer = candidateList.FirstOrDefault();
                    if (targetReviewer == null) break;

                    var targetUser = await _context.Users.FirstOrDefaultAsync(u => u.Id == targetReviewer.ReviewerId);

                    results.Add(new CriteriaReassignmentResultDto
                    {
                        ApplicationId = assignment.ApplicationId,
                        ApplicationRefNumber = assignment.Application?.ApplicationNumber,
                        PreviousReviewerId = reviewerId,
                        PreviousReviewerName = assignment.Reviewer?.FirstName + " " + assignment.Reviewer?.LastName,
                        NewReviewerId = targetReviewer.ReviewerId,
                        NewReviewerName = targetUser?.FirstName + " " + targetUser?.LastName,
                        ReassignmentReason = $"High workload: {reviewerWorkload} items, move to reviewer with {targetReviewer.Workload} items",
                        Success = true
                    });
                }

                return results;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error getting reassignment candidates for reviewer {reviewerId}: {ex.Message}");
                throw;
            }
        }

        public async Task<List<CriteriaReassignmentResultDto>> ReassignFromUnavailableReviewersAsync()
        {
            try
            {
                var results = new List<CriteriaReassignmentResultDto>();

                var unavailableReviewers = await _availabilityService.GetUnavailableReviewersAsync();
                var unavailableReviewerIds = unavailableReviewers.Select(r => r.ReviewerId).ToList();

                if (!unavailableReviewerIds.Any())
                {
                    _logger.LogInformation("No unavailable reviewers found for reassignment");
                    return results;
                }

                var assignmentsToReassign = await _context.ApplicationAssignments
                    .Where(a => unavailableReviewerIds.Contains(a.ReviewerId) && a.IsActive)
                    .Include(a => a.Application)
                    .Include(a => a.Reviewer)
                    .ToListAsync();

                // Get available reviewers with lowest workload
                var availableReviewers = await _context.ApplicationAssignments
                    .Where(a => !unavailableReviewerIds.Contains(a.ReviewerId) && a.IsActive)
                    .GroupBy(a => a.ReviewerId)
                    .Select(g => new { ReviewerId = g.Key, Workload = g.Count() })
                    .OrderBy(g => g.Workload)
                    .ToListAsync();

                var assignmentIndex = 0;
                foreach (var assignment in assignmentsToReassign)
                {
                    var targetReviewer = availableReviewers[assignmentIndex % availableReviewers.Count];

                    var reassignmentResult = await ExecuteReassignmentAsync(
                        assignment,
                        targetReviewer.ReviewerId,
                        "AvailabilityChange",
                        $"Original reviewer unavailable");

                    results.Add(reassignmentResult);
                    assignmentIndex++;
                }

                _logger.LogInformation($"Reassigned {results.Count(r => r.Success)} applications from unavailable reviewers");

                return results;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error reassigning from unavailable reviewers: {ex.Message}");
                throw;
            }
        }

        public async Task<List<CriteriaReassignmentResultDto>> AutoBalanceWorkloadAsync()
        {
            var criteria = new ReassignmentCriteriaDto
            {
                WorkloadThresholdPercentage = 75,
                ReassignIfUnavailable = true,
                TargetWorkloadMaxPercentage = 50,
                PrioritizeHighPerformers = true,
                MinimumApprovalRatePercentage = 70,
                MaxReassignments = 50
            };

            return await ExecuteReassignmentsByCriteriaAsync(criteria);
        }

        public async Task<(bool IsValid, string Reason)> ValidateReassignmentAsync(Guid applicationId, Guid currentReviewerId, Guid newReviewerId)
        {
            try
            {
                // Check if application exists
                var application = await _context.Applications.FirstOrDefaultAsync(a => a.Id == applicationId);
                if (application == null)
                    return (false, "Application not found");

                // Check if current assignment exists
                var currentAssignment = await _context.ApplicationAssignments
                    .FirstOrDefaultAsync(a => a.ApplicationId == applicationId && a.ReviewerId == currentReviewerId);
                if (currentAssignment == null)
                    return (false, "Current assignment not found");

                // Check if new reviewer exists
                var newReviewer = await _context.Users.FirstOrDefaultAsync(u => u.Id == newReviewerId);
                if (newReviewer == null)
                    return (false, "New reviewer not found");

                // Check if new reviewer is available
                var isAvailable = await _availabilityService.IsReviewerAvailableAsync(newReviewerId);
                if (!isAvailable)
                    return (false, "New reviewer is currently unavailable");

                // Check if new reviewer has admin role (per assignment rules)
                if (newReviewer.Role != "ADMIN")
                    return (false, "Target reviewer must have ADMIN role");

                return (true, "Reassignment is valid");
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error validating reassignment: {ex.Message}");
                return (false, $"Validation error: {ex.Message}");
            }
        }

        private List<dynamic> GetEligibleTargetReviewers(
            List<dynamic> reviewerWorkloads,
            Guid excludeReviewerId,
            double targetMaxWorkload,
            ReassignmentCriteriaDto criteria,
            List<ReviewerPerformanceMetrics> performanceMetrics)
        {
            var candidates = new List<dynamic>();

            foreach (var reviewer in reviewerWorkloads.Where(r => r.ReviewerId != excludeReviewerId && r.Workload < targetMaxWorkload))
            {
                var performance = performanceMetrics.FirstOrDefault(p => p.ReviewerId == reviewer.ReviewerId);

                if (criteria.PrioritizeHighPerformers && performance != null &&
                    performance.ApprovalRatePercentage >= criteria.MinimumApprovalRatePercentage)
                {
                    candidates.Add(reviewer);
                }
                else if (!criteria.PrioritizeHighPerformers)
                {
                    candidates.Add(reviewer);
                }
            }

            return candidates.OrderBy(c => c.Workload).ToList();
        }

        private async Task<CriteriaReassignmentResultDto> ExecuteReassignmentAsync(
            ApplicationAssignment assignment,
            Guid newReviewerId,
            string reason,
            string details)
        {
            try
            {
                var newReviewer = await _context.Users.FirstOrDefaultAsync(u => u.Id == newReviewerId);

                // Create reassignment history entry
                var reassignmentHistory = new ReassignmentHistory
                {
                    ApplicationAssignmentId = assignment.Id,
                    ApplicationId = assignment.ApplicationId,
                    PreviousReviewerId = assignment.ReviewerId,
                    NewReviewerId = newReviewerId,
                    ReassignmentReason = reason,
                    ReassignmentDetails = details,
                    ReassignedAt = DateTime.UtcNow,
                    PreviousAssignmentStatus = assignment.IsActive ? "Active" : "Inactive",
                    IsCompleted = true
                };

                // Update assignment
                assignment.ReviewerId = newReviewerId;
                assignment.AssignedAt = DateTime.UtcNow;
                reassignmentHistory.NewAssignmentStatus = assignment.IsActive ? "Active" : "Inactive";

                _context.ReassignmentHistories.Add(reassignmentHistory);
                await _context.SaveChangesAsync();

                return new CriteriaReassignmentResultDto
                {
                    ApplicationId = assignment.ApplicationId,
                    ApplicationRefNumber = assignment.Application?.ApplicationNumber,
                    PreviousReviewerId = assignment.ReviewerId,
                    PreviousReviewerName = assignment.Reviewer?.FirstName + " " + assignment.Reviewer?.LastName,
                    NewReviewerId = newReviewerId,
                    NewReviewerName = newReviewer?.FirstName + " " + newReviewer?.LastName,
                    ReassignmentReason = reason,
                    Success = true
                };
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error executing reassignment for application {assignment.ApplicationId}: {ex.Message}");
                return new CriteriaReassignmentResultDto
                {
                    ApplicationId = assignment.ApplicationId,
                    ApplicationRefNumber = assignment.Application?.ApplicationNumber,
                    PreviousReviewerId = assignment.ReviewerId,
                    Success = false,
                    ErrorMessage = ex.Message
                };
            }
        }
    }
}
