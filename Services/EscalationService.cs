using Microsoft.EntityFrameworkCore;
using MMGT.API.Data;
using MMGT.API.Models;
using MMGT.API.DTOs.Escalation;
using MMGT.API.DTOs.Assignment;

namespace MMGT.API.Services
{
    public interface IEscalationService
    {
        // Rule Management
        Task<EscalationRuleDto> CreateEscalationRuleAsync(CreateEscalationRuleRequest request, Guid createdBy);
        Task<EscalationRuleDto> UpdateEscalationRuleAsync(Guid ruleId, UpdateEscalationRuleRequest request, Guid modifiedBy);
        Task<bool> DeleteEscalationRuleAsync(Guid ruleId);
        Task<EscalationRuleDto> GetEscalationRuleAsync(Guid ruleId);
        Task<List<EscalationRuleDto>> GetAllEscalationRulesAsync();
        Task<bool> ToggleEscalationRuleAsync(Guid ruleId, bool isActive);

        // Escalation Detection & Action
        Task CheckAndEscalateDelayedAssignmentsAsync();
        Task<AssignmentEscalationDto> EscalateAssignmentAsync(Guid assignmentId, Guid escalationRuleId);
        Task<bool> ResolveEscalationAsync(Guid escalationId, string resolutionComments, Guid resolvedBy);
        Task<List<AssignmentEscalationDto>> GetActiveEscalationsAsync();
        Task<List<AssignmentEscalationDto>> GetEscalationsForReviewerAsync(Guid reviewerId);

        // Reassignment Management (LEAD_REVIEWER only)
        Task<bool> ReassignApplicationAsync(Guid assignmentId, Guid newReviewerId, string reason);
        Task<bool> ReassignMultipleApplicationsAsync(List<Guid> assignmentIds, Guid newReviewerId, string reason);
        Task<List<ApplicationAssignment>> GetAssignmentsForReviewerAsync(Guid reviewerId);
        Task<PaginatedApplicationWithAssignmentDto> GetApplicationsForBulkReassignmentAsync(
            int pageNumber,
            int pageSize,
            string? sortBy,
            bool sortDescending,
            Guid? reviewerId,
            string? applicationStatus,
            string? rsaPin = null,
            string? applicationNumber = null,
            DateTime? createdDateFrom = null,
            DateTime? createdDateTo = null,
            DateTime? submittedDateFrom = null,
            DateTime? submittedDateTo = null);
    }

    public class EscalationService : IEscalationService
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<EscalationService> _logger;
        private readonly IReviewerPerformanceService _performanceService;

        public EscalationService(
            ApplicationDbContext context,
            ILogger<EscalationService> logger,
            IReviewerPerformanceService performanceService)
        {
            _context = context;
            _logger = logger;
            _performanceService = performanceService;
        }

        #region Rule Management

        /// <summary>
        /// Create a new escalation rule
        /// </summary>
        public async Task<EscalationRuleDto> CreateEscalationRuleAsync(CreateEscalationRuleRequest request, Guid createdBy)
        {
            var rule = new EscalationRule
            {
                RuleName = request.RuleName,
                Description = request.Description,
                ThresholdHours = request.ThresholdHours,
                ApplicablePriority = request.ApplicablePriority,
                EscalationAction = request.EscalationAction,
                NotifyEmails = request.NotifyEmails,
                IsActive = true,
                CreatedBy = createdBy,
                CreatedAt = DateTime.UtcNow
            };

            _context.EscalationRules.Add(rule);
            await _context.SaveChangesAsync();

            _logger.LogInformation($"Escalation rule '{rule.RuleName}' created by {createdBy}");
            return MapRuleToDto(rule);
        }

        /// <summary>
        /// Update an escalation rule
        /// </summary>
        public async Task<EscalationRuleDto> UpdateEscalationRuleAsync(Guid ruleId, UpdateEscalationRuleRequest request, Guid modifiedBy)
        {
            var rule = await _context.EscalationRules.FindAsync(ruleId);
            if (rule == null)
                throw new InvalidOperationException($"Escalation rule {ruleId} not found");

            rule.RuleName = request.RuleName ?? rule.RuleName;
            rule.Description = request.Description ?? rule.Description;
            rule.ThresholdHours = request.ThresholdHours ?? rule.ThresholdHours;
            rule.ApplicablePriority = request.ApplicablePriority ?? rule.ApplicablePriority;
            rule.EscalationAction = request.EscalationAction ?? rule.EscalationAction;
            rule.NotifyEmails = request.NotifyEmails ?? rule.NotifyEmails;
            rule.ModifiedBy = modifiedBy;
            rule.ModifiedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();
            _logger.LogInformation($"Escalation rule '{rule.RuleName}' updated by {modifiedBy}");
            return MapRuleToDto(rule);
        }

        /// <summary>
        /// Delete an escalation rule
        /// </summary>
        public async Task<bool> DeleteEscalationRuleAsync(Guid ruleId)
        {
            var rule = await _context.EscalationRules.FindAsync(ruleId);
            if (rule == null)
                return false;

            _context.EscalationRules.Remove(rule);
            await _context.SaveChangesAsync();
            _logger.LogInformation($"Escalation rule {ruleId} deleted");
            return true;
        }

        /// <summary>
        /// Get a specific escalation rule
        /// </summary>
        public async Task<EscalationRuleDto> GetEscalationRuleAsync(Guid ruleId)
        {
            var rule = await _context.EscalationRules.FindAsync(ruleId);
            if (rule == null)
                throw new InvalidOperationException($"Escalation rule {ruleId} not found");

            return MapRuleToDto(rule);
        }

        /// <summary>
        /// Get all escalation rules
        /// </summary>
        public async Task<List<EscalationRuleDto>> GetAllEscalationRulesAsync()
        {
            var rules = await _context.EscalationRules
                .AsNoTracking()
                .OrderBy(r => r.CreatedAt)
                .ToListAsync();

            return rules.Select(MapRuleToDto).ToList();
        }

        /// <summary>
        /// Toggle escalation rule active status
        /// </summary>
        public async Task<bool> ToggleEscalationRuleAsync(Guid ruleId, bool isActive)
        {
            var rule = await _context.EscalationRules.FindAsync(ruleId);
            if (rule == null)
                return false;

            rule.IsActive = isActive;
            await _context.SaveChangesAsync();
            _logger.LogInformation($"Escalation rule {ruleId} active status set to {isActive}");
            return true;
        }

        #endregion

        #region Escalation Detection & Action

        /// <summary>
        /// Check all assignments and escalate those that exceed thresholds
        /// This should be called periodically (e.g., every hour) by a background job
        /// </summary>
        public async Task CheckAndEscalateDelayedAssignmentsAsync()
        {
            try
            {
                var activeRules = await _context.EscalationRules
                    .Where(r => r.IsActive)
                    .ToListAsync();

                if (!activeRules.Any())
                {
                    _logger.LogInformation("No active escalation rules found");
                    return;
                }

                // Get all active assignments
                var activeAssignments = await _context.ApplicationAssignments
                    .Where(a => a.IsActive)
                    .Include(a => a.Application)
                    .Include(a => a.Reviewer)
                    .ToListAsync();

                var now = DateTime.UtcNow;
                var escalationCount = 0;

                foreach (var assignment in activeAssignments)
                {
                    if (assignment.Application == null)
                        continue;

                    var hoursElapsed = (now - assignment.AssignedAt).TotalHours;
                    var appPriority = assignment.Application.Priority.ToLower();

                    // Check each rule
                    foreach (var rule in activeRules)
                    {
                        // Check if rule applies to this priority
                        if (rule.ApplicablePriority != "all" && rule.ApplicablePriority != appPriority)
                            continue;

                        // Check if assignment exceeds threshold
                        if (hoursElapsed > rule.ThresholdHours)
                        {
                            // Check if already escalated for this rule
                            var existingEscalation = await _context.AssignmentEscalations
                                .FirstOrDefaultAsync(e => 
                                    e.ApplicationAssignmentId == assignment.Id && 
                                    e.EscalationRuleId == rule.Id &&
                                    e.Status != "resolved");

                            if (existingEscalation == null)
                            {
                                // Create new escalation
                                var escalation = new AssignmentEscalation
                                {
                                    ApplicationAssignmentId = assignment.Id,
                                    EscalationRuleId = rule.Id,
                                    EscalatedAt = now,
                                    Status = "triggered",
                                    HoursOverThreshold = hoursElapsed - rule.ThresholdHours
                                };

                                _context.AssignmentEscalations.Add(escalation);
                                escalationCount++;

                                // Perform escalation action
                                await PerformEscalationActionAsync(escalation, rule, assignment);
                            }
                        }
                    }
                }

                await _context.SaveChangesAsync();
                _logger.LogInformation($"Escalation check completed: {escalationCount} new escalations triggered");
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error checking and escalating delayed assignments: {ex.Message}");
            }
        }

        /// <summary>
        /// Perform the escalation action defined in the rule
        /// </summary>
        private async Task PerformEscalationActionAsync(AssignmentEscalation escalation, EscalationRule rule, ApplicationAssignment assignment)
        {
            try
            {
                switch (rule.EscalationAction.ToLower())
                {
                    case "notify":
                        await NotifyEscalationAsync(escalation, rule, assignment);
                        escalation.Status = "notified";
                        break;

                    case "reassign":
                        // Reassign to reviewer with lowest workload (excluding current reviewer)
                        await ReassignOnEscalationAsync(escalation, rule, assignment);
                        escalation.Status = "reassigned";
                        break;

                    case "escalate_to_lead":
                        await EscalateToLeadReviewerAsync(escalation, rule, assignment);
                        escalation.Status = "escalated_to_lead";
                        break;

                    default:
                        _logger.LogWarning($"Unknown escalation action: {rule.EscalationAction}");
                        break;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error performing escalation action: {ex.Message}");
            }
        }

        /// <summary>
        /// Send notification for escalation
        /// </summary>
        private async Task NotifyEscalationAsync(AssignmentEscalation escalation, EscalationRule rule, ApplicationAssignment assignment)
        {
            try
            {
                var notifyEmails = rule.NotifyEmails?.Split(';')
                    .Select(e => e.Trim())
                    .Where(e => !string.IsNullOrEmpty(e))
                    .ToList() ?? new List<string>();

                if (notifyEmails.Any())
                {
                    var reviewerName = assignment.Reviewer != null 
                        ? $"{assignment.Reviewer.FirstName} {assignment.Reviewer.LastName}" 
                        : "Unknown Reviewer";

                    var hoursOver = (int)Math.Round(escalation.HoursOverThreshold);

                    foreach (var email in notifyEmails)
                    {
                        try
                        {
                            // Log escalation notification (email service can be extended later)
                            _logger.LogWarning($"Escalation notification: {rule.RuleName} triggered for assignment {assignment.Id}. " +
                                $"Reviewer: {reviewerName}, Hours over threshold: {hoursOver}, Recipient: {email}");
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError($"Error logging escalation notification to {email}: {ex.Message}");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error notifying escalation: {ex.Message}");
            }
        }

        /// <summary>
        /// Reassign application to reviewer with lowest workload
        /// </summary>
        private async Task ReassignOnEscalationAsync(AssignmentEscalation escalation, EscalationRule rule, ApplicationAssignment assignment)
        {
            try
            {
                var reviewers = await _context.Users
                    .Where(u => u.IsActive && u.Id != assignment.ReviewerId && (u.Role == "ADMIN" || u.Role == "LEAD_REVIEWER"))
                    .ToListAsync();

                if (!reviewers.Any())
                {
                    _logger.LogWarning("No alternative reviewers available for reassignment");
                    return;
                }

                // Find reviewer with lowest workload
                var lowestWorkloadReviewer = reviewers.First();
                var lowestWorkload = await _context.ApplicationAssignments
                    .Where(a => a.ReviewerId == lowestWorkloadReviewer.Id && a.IsActive)
                    .CountAsync();

                foreach (var reviewer in reviewers.Skip(1))
                {
                    var workload = await _context.ApplicationAssignments
                        .Where(a => a.ReviewerId == reviewer.Id && a.IsActive)
                        .CountAsync();

                    if (workload < lowestWorkload)
                    {
                        lowestWorkload = workload;
                        lowestWorkloadReviewer = reviewer;
                    }
                }

                // Create new assignment
                var newAssignment = new ApplicationAssignment
                {
                    ApplicationId = assignment.ApplicationId,
                    ReviewerId = lowestWorkloadReviewer.Id,
                    AssignedBy = null, // System reassignment
                    AssignedAt = DateTime.UtcNow,
                    ReassignmentReason = $"Escalated due to {rule.RuleName}",
                    IsActive = true
                };

                // Deactivate old assignment
                assignment.IsActive = false;

                _context.ApplicationAssignments.Add(newAssignment);
                escalation.ReassignedToReviewerId = lowestWorkloadReviewer.Id;
                escalation.ActionReason = $"Reassigned from {assignment.Reviewer?.FirstName} {assignment.Reviewer?.LastName} to {lowestWorkloadReviewer.FirstName} {lowestWorkloadReviewer.LastName}";

                _logger.LogInformation($"Assignment {assignment.Id} reassigned on escalation");
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error reassigning on escalation: {ex.Message}");
            }
        }

        /// <summary>
        /// Escalate to lead reviewer (send notification to all lead reviewers)
        /// </summary>
        private async Task EscalateToLeadReviewerAsync(AssignmentEscalation escalation, EscalationRule rule, ApplicationAssignment assignment)
        {
            try
            {
                var leadReviewers = await _context.Users
                    .Where(u => u.IsActive && u.Role == "LEAD_REVIEWER")
                    .ToListAsync();

                foreach (var leadReviewer in leadReviewers)
                {
                    try
                    {
                        // Log escalation to lead reviewer (email service can be extended later)
                        _logger.LogWarning($"Escalation to Lead Reviewer: {rule.RuleName} for assignment {assignment.Id}. " +
                            $"Application: {assignment.ApplicationId}, Current Reviewer: {assignment.Reviewer?.Email ?? "Unknown"}, " +
                            $"Lead Reviewer Notified: {leadReviewer.Email}");
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError($"Error logging escalation to lead reviewer {leadReviewer.Email}: {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error escalating to lead reviewer: {ex.Message}");
            }
        }

        /// <summary>
        /// Escalate an assignment manually
        /// </summary>
        public async Task<AssignmentEscalationDto> EscalateAssignmentAsync(Guid assignmentId, Guid escalationRuleId)
        {
            var assignment = await _context.ApplicationAssignments.FindAsync(assignmentId);
            if (assignment == null)
                throw new InvalidOperationException($"Assignment {assignmentId} not found");

            var rule = await _context.EscalationRules.FindAsync(escalationRuleId);
            if (rule == null)
                throw new InvalidOperationException($"Escalation rule {escalationRuleId} not found");

            var escalation = new AssignmentEscalation
            {
                ApplicationAssignmentId = assignmentId,
                EscalationRuleId = escalationRuleId,
                EscalatedAt = DateTime.UtcNow,
                Status = "triggered",
                HoursOverThreshold = (DateTime.UtcNow - assignment.AssignedAt).TotalHours
            };

            _context.AssignmentEscalations.Add(escalation);
            await _context.SaveChangesAsync();

            _logger.LogInformation($"Assignment {assignmentId} manually escalated");
            return MapEscalationToDto(escalation);
        }

        /// <summary>
        /// Resolve an escalation
        /// </summary>
        public async Task<bool> ResolveEscalationAsync(Guid escalationId, string resolutionComments, Guid resolvedBy)
        {
            var escalation = await _context.AssignmentEscalations.FindAsync(escalationId);
            if (escalation == null)
                return false;

            escalation.Status = "resolved";
            escalation.ResolvedAt = DateTime.UtcNow;
            escalation.ResolutionComments = resolutionComments;
            escalation.ResolvedBy = resolvedBy;

            await _context.SaveChangesAsync();
            _logger.LogInformation($"Escalation {escalationId} resolved");
            return true;
        }

        /// <summary>
        /// Get all active escalations
        /// </summary>
        public async Task<List<AssignmentEscalationDto>> GetActiveEscalationsAsync()
        {
            var escalations = await _context.AssignmentEscalations
                .AsNoTracking()
                .Where(e => e.Status != "resolved")
                .Include(e => e.ApplicationAssignment)
                .Include(e => e.EscalationRule)
                .OrderByDescending(e => e.EscalatedAt)
                .ToListAsync();

            return escalations.Select(MapEscalationToDto).ToList();
        }

        /// <summary>
        /// Get escalations for a specific reviewer
        /// </summary>
        public async Task<List<AssignmentEscalationDto>> GetEscalationsForReviewerAsync(Guid reviewerId)
        {
            var escalations = await _context.AssignmentEscalations
                .AsNoTracking()
                .Where(e => e.ApplicationAssignment!.ReviewerId == reviewerId && e.Status != "resolved")
                .Include(e => e.ApplicationAssignment)
                .Include(e => e.EscalationRule)
                .OrderByDescending(e => e.EscalatedAt)
                .ToListAsync();

            return escalations.Select(MapEscalationToDto).ToList();
        }

        #endregion

        #region Helper Methods

        private EscalationRuleDto MapRuleToDto(EscalationRule rule)
        {
            return new EscalationRuleDto
            {
                Id = rule.Id,
                RuleName = rule.RuleName,
                Description = rule.Description,
                ThresholdHours = rule.ThresholdHours,
                ApplicablePriority = rule.ApplicablePriority,
                EscalationAction = rule.EscalationAction,
                NotifyEmails = rule.NotifyEmails,
                IsActive = rule.IsActive,
                CreatedAt = rule.CreatedAt,
                CreatedBy = rule.CreatedBy,
                ModifiedAt = rule.ModifiedAt,
                ModifiedBy = rule.ModifiedBy
            };
        }

        private AssignmentEscalationDto MapEscalationToDto(AssignmentEscalation escalation)
        {
            return new AssignmentEscalationDto
            {
                Id = escalation.Id,
                ApplicationAssignmentId = escalation.ApplicationAssignmentId,
                EscalationRuleId = escalation.EscalationRuleId,
                EscalatedAt = escalation.EscalatedAt,
                Status = escalation.Status,
                HoursOverThreshold = Math.Round(escalation.HoursOverThreshold, 2),
                ReassignedToReviewerId = escalation.ReassignedToReviewerId,
                ActionReason = escalation.ActionReason,
                ResolvedAt = escalation.ResolvedAt,
                ResolutionComments = escalation.ResolutionComments,
                ResolvedBy = escalation.ResolvedBy,
                RuleName = escalation.EscalationRule?.RuleName,
                ApplicationId = escalation.ApplicationAssignment?.ApplicationId
            };
        }

        #endregion

        #region Reassignment Management

        /// <summary>
        /// Reassign a single application from one reviewer to another (LEAD_REVIEWER only)
        /// </summary>
        public async Task<bool> ReassignApplicationAsync(Guid assignmentId, Guid newReviewerId, string reason)
        {
            try
            {
                var assignment = await _context.ApplicationAssignments
                    .Include(a => a.Application)
                    .FirstOrDefaultAsync(a => a.Id == assignmentId);

                if (assignment == null)
                {
                    _logger.LogWarning($"Assignment {assignmentId} not found for reassignment");
                    return false;
                }

                // Check if new reviewer exists and is active
                var newReviewer = await _context.Users
                    .FirstOrDefaultAsync(u => u.Id == newReviewerId && u.IsActive && (u.Role == "ADMIN" || u.Role == "LEAD_REVIEWER"));

                if (newReviewer == null)
                {
                    _logger.LogWarning($"New reviewer {newReviewerId} not found or not active");
                    return false;
                }

                // Store old reviewer for tracking
                var oldReviewerId = assignment.ReviewerId;

                // Create reassignment history entry for audit trail
                var reassignmentHistory = new ReassignmentHistory
                {
                    ApplicationAssignmentId = assignment.Id,
                    ApplicationId = assignment.ApplicationId,
                    PreviousReviewerId = oldReviewerId,
                    NewReviewerId = newReviewerId,
                    ReassignmentReason = reason,
                    ReassignmentDetails = reason,
                    ReassignedAt = DateTime.UtcNow,
                    PreviousAssignmentStatus = assignment.IsActive ? "Active" : "Inactive",
                    IsCompleted = true
                };

                // Update the assignment
                assignment.ReviewerId = newReviewerId;
                assignment.AssignedAt = DateTime.UtcNow;
                assignment.ReassignmentReason = reason;
                reassignmentHistory.NewAssignmentStatus = assignment.IsActive ? "Active" : "Inactive";

                // Save both reassignment history and assignment update
                _context.ReassignmentHistories.Add(reassignmentHistory);
                _logger.LogInformation($"Application assignment {assignmentId} reassigned from reviewer {oldReviewerId} to {newReviewerId}. Reason: {reason}");

                await _context.SaveChangesAsync();

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error reassigning application {assignmentId}: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Reassign multiple applications from one reviewer to another (LEAD_REVIEWER only - bulk operation)
        /// </summary>
        public async Task<bool> ReassignMultipleApplicationsAsync(List<Guid> assignmentIds, Guid newReviewerId, string reason)
        {
            try
            {
                // Check if new reviewer exists and is active
                var newReviewer = await _context.Users
                    .FirstOrDefaultAsync(u => u.Id == newReviewerId && u.IsActive && (u.Role == "ADMIN" || u.Role == "LEAD_REVIEWER"));

                if (newReviewer == null)
                {
                    _logger.LogWarning($"New reviewer {newReviewerId} not found or not active");
                    return false;
                }

                // Materialize the assignmentIds list to avoid EF Core generating problematic OPENJSON WITH SQL
                var idSet = new HashSet<Guid>(assignmentIds);

                var assignments = await _context.ApplicationAssignments
                    .Include(a => a.Application)
                    .ToListAsync();

                // Filter in memory to avoid EF Core translation issues
                var matchingAssignments = assignments.Where(a => idSet.Contains(a.Id)).ToList();

                if (!matchingAssignments.Any())
                {
                    _logger.LogWarning($"No assignments found for the provided IDs");
                    return false;
                }

                int reassignedCount = 0;
                var reassignmentHistories = new List<ReassignmentHistory>();
                var oldReviewerIds = new HashSet<Guid>();

                foreach (var assignment in matchingAssignments)
                {
                    var oldReviewerId = assignment.ReviewerId;
                    oldReviewerIds.Add(oldReviewerId);

                    // Create reassignment history entry for audit trail
                    var reassignmentHistory = new ReassignmentHistory
                    {
                        ApplicationAssignmentId = assignment.Id,
                        ApplicationId = assignment.ApplicationId,
                        PreviousReviewerId = oldReviewerId,
                        NewReviewerId = newReviewerId,
                        ReassignmentReason = reason,
                        ReassignmentDetails = reason,
                        ReassignedAt = DateTime.UtcNow,
                        PreviousAssignmentStatus = assignment.IsActive ? "Active" : "Inactive",
                        IsCompleted = true
                    };

                    // Deactivate the old assignment
                    assignment.IsActive = false;
                    _context.ApplicationAssignments.Update(assignment);

                    // Create a new assignment for the new reviewer
                    var newAssignment = new ApplicationAssignment
                    {
                        Id = Guid.NewGuid(),
                        ApplicationId = assignment.ApplicationId,
                        ReviewerId = newReviewerId,
                        AssignedBy = null, // System-initiated reassignment
                        AssignedAt = DateTime.UtcNow,
                        ReassignmentReason = reason,
                        IsActive = true
                    };
                    _context.ApplicationAssignments.Add(newAssignment);

                    reassignmentHistory.NewAssignmentStatus = "Active";
                    reassignmentHistories.Add(reassignmentHistory);
                    reassignedCount++;

                    _logger.LogInformation($"Application assignment {assignment.Id} deactivated and reassigned from reviewer {oldReviewerId} to {newReviewerId}. New assignment: {newAssignment.Id}. Reason: {reason}");
                }

                // Save all reassignment histories and assignment updates together
                foreach (var history in reassignmentHistories)
                {
                    _context.ReassignmentHistories.Add(history);
                }

                await _context.SaveChangesAsync();

                // Recalculate metrics for both old and new reviewers
                oldReviewerIds.Add(newReviewerId);
                foreach (var reviewerId in oldReviewerIds)
                {
                    await _performanceService.RecalculateMetricsAsync(reviewerId);
                    _logger.LogInformation($"Metrics recalculated for reviewer {reviewerId} after bulk reassignment");
                }

                _logger.LogInformation($"Successfully reassigned {reassignedCount} applications to reviewer {newReviewerId}");
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error reassigning multiple applications: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Get all assignments (active and inactive) for a specific reviewer
        /// </summary>
        public async Task<List<ApplicationAssignment>> GetAssignmentsForReviewerAsync(Guid reviewerId)
        {
            try
            {
                var assignments = await _context.ApplicationAssignments
                    .Where(a => a.ReviewerId == reviewerId)
                    .Include(a => a.Application)
                    .Include(a => a.Reviewer)
                    .OrderByDescending(a => a.AssignedAt)
                    .ToListAsync();

                return assignments;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error getting assignments for reviewer {reviewerId}: {ex.Message}");
                return new List<ApplicationAssignment>();
            }
        }

        /// <summary>
        /// Get all applications with their current assignment details for bulk reassignment
        /// </summary>
        public async Task<PaginatedApplicationWithAssignmentDto> GetApplicationsForBulkReassignmentAsync(
            int pageNumber,
            int pageSize,
            string? sortBy,
            bool sortDescending,
            Guid? reviewerId,
            string? applicationStatus,
            string? rsaPin = null,
            string? applicationNumber = null,
            DateTime? createdDateFrom = null,
            DateTime? createdDateTo = null,
            DateTime? submittedDateFrom = null,
            DateTime? submittedDateTo = null)
        {
            try
            {
                pageNumber = Math.Max(1, pageNumber);
                pageSize = Math.Max(1, Math.Min(pageSize, 100)); // Cap at 100

                // Get all applications with their current active assignments
                var query = _context.ApplicationAssignments
                    .Where(a => a.IsActive)
                    .Include(a => a.Application)
                    .Include(a => a.Reviewer)
                    .AsNoTracking();

                // Filter by reviewer if specified
                if (reviewerId.HasValue && reviewerId != Guid.Empty)
                {
                    query = query.Where(a => a.ReviewerId == reviewerId.Value);
                }

                // Filter by application status if specified
                if (!string.IsNullOrEmpty(applicationStatus))
                {
                    query = query.Where(a => a.Application != null && a.Application.Status == applicationStatus);
                }

                // Filter by RSA PIN if specified (case-insensitive partial match)
                if (!string.IsNullOrEmpty(rsaPin))
                {
                    query = query.Where(a => a.Application != null && a.Application.RsaPin.ToLower().Contains(rsaPin.ToLower()));
                }

                // Filter by Application Number if specified (case-insensitive partial match)
                if (!string.IsNullOrEmpty(applicationNumber))
                {
                    query = query.Where(a => a.Application != null && a.Application.ApplicationNumber.ToLower().Contains(applicationNumber.ToLower()));
                }

                // Filter by Created Date Range if specified
                if (createdDateFrom.HasValue)
                {
                    query = query.Where(a => a.Application != null && a.Application.CreatedAt >= createdDateFrom.Value);
                }
                if (createdDateTo.HasValue)
                {
                    // Add 1 day to include the entire end date
                    query = query.Where(a => a.Application != null && a.Application.CreatedAt < createdDateTo.Value.AddDays(1));
                }

                // Filter by Submitted Date Range if specified
                if (submittedDateFrom.HasValue)
                {
                    query = query.Where(a => a.Application != null && a.Application.SubmittedAt >= submittedDateFrom.Value);
                }
                if (submittedDateTo.HasValue)
                {
                    // Add 1 day to include the entire end date
                    query = query.Where(a => a.Application != null && a.Application.SubmittedAt < submittedDateTo.Value.AddDays(1));
                }

                // Apply sorting
                query = sortBy?.ToLower() switch
                {
                    "assignedfohours" or "duration" => sortDescending
                        ? query.OrderByDescending(a => EF.Functions.DateDiffSecond(a.AssignedAt, DateTime.UtcNow))
                        : query.OrderBy(a => EF.Functions.DateDiffSecond(a.AssignedAt, DateTime.UtcNow)),

                    "reviewername" => sortDescending
                        ? query.OrderByDescending(a => a.Reviewer!.FirstName).ThenByDescending(a => a.Reviewer!.LastName)
                        : query.OrderBy(a => a.Reviewer!.FirstName).ThenBy(a => a.Reviewer!.LastName),

                    "borrowername" => sortDescending
                        ? query.OrderByDescending(a => a.Application!.FirstName).ThenByDescending(a => a.Application!.LastName)
                        : query.OrderBy(a => a.Application!.FirstName).ThenBy(a => a.Application!.LastName),

                    "priority" => sortDescending
                        ? query.OrderByDescending(a => a.Application!.Priority)
                        : query.OrderBy(a => a.Application!.Priority),

                    _ => query.OrderBy(a => a.Application!.ApplicationNumber)
                };

                // Get total count before pagination
                var totalCount = await query.CountAsync();
                var totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);

                // Apply pagination
                var assignments = await query
                    .Skip((pageNumber - 1) * pageSize)
                    .Take(pageSize)
                    .ToListAsync();

                // Map to DTO
                var now = DateTime.UtcNow;
                var applicationDtos = assignments.Select(a => new ApplicationWithAssignmentDto
                {
                    ApplicationId = a.ApplicationId,
                    ApplicationNumber = a.Application?.ApplicationNumber ?? string.Empty,
                    ApplicationStatus = a.Application?.Status ?? string.Empty,
                    ApplicationPriority = a.Application?.Priority ?? string.Empty,
                    BorrowerFirstName = a.Application?.FirstName ?? string.Empty,
                    BorrowerLastName = a.Application?.LastName ?? string.Empty,
                    RsaPin = a.Application?.RsaPin ?? string.Empty,
                    LoanAmount = a.Application?.LoanAmount ?? 0,
                    AssignmentId = a.Id,
                    ReviewerId = a.ReviewerId,
                    ReviewerName = a.Reviewer != null ? $"{a.Reviewer.FirstName} {a.Reviewer.LastName}" : "Unknown",
                    ReviewerEmail = a.Reviewer?.Email ?? string.Empty,
                    AssignedAt = a.AssignedAt,
                    AssignedForHours = Math.Round((now - a.AssignedAt).TotalHours, 2),
                    IsActive = a.IsActive,
                    ReassignmentReason = a.ReassignmentReason,
                    AssignedBy = a.AssignedBy ?? Guid.Empty,
                    ApplicationCreatedAt = a.Application?.CreatedAt ?? DateTime.MinValue,
                    ApplicationSubmittedAt = a.Application?.SubmittedAt
                }).ToList();

                // Calculate summary statistics
                var allAssignments = await _context.ApplicationAssignments
                    .Where(a => a.IsActive)
                    .Include(a => a.Application)
                    .Include(a => a.Reviewer)
                    .AsNoTracking()
                    .ToListAsync();

                var reviewerWorkloads = allAssignments
                    .GroupBy(a => new { a.ReviewerId, ReviewerName = a.Reviewer != null ? $"{a.Reviewer.FirstName} {a.Reviewer.LastName}" : "Unknown", a.Reviewer!.Email })
                    .Select(g => new ReviewerWorkloadSummaryDto
                    {
                        ReviewerId = g.Key.ReviewerId,
                        ReviewerName = g.Key.ReviewerName,
                        ReviewerEmail = g.Key.Email ?? string.Empty,
                        AssignmentCount = g.Count(),
                        AverageAssignedHours = Math.Round(g.Average(a => (now - a.AssignedAt).TotalHours), 2)
                    })
                    .ToList();

                var summary = new AssignmentSummaryDto
                {
                    TotalActiveApplications = totalCount,
                    TotalAssignments = totalCount,
                    UniqueReviewerCount = reviewerWorkloads.Count,
                    AverageAssignedHours = allAssignments.Any() ? Math.Round(allAssignments.Average(a => (now - a.AssignedAt).TotalHours), 2) : 0,
                    OverdueApplicationsCount = allAssignments.Count(a => (now - a.AssignedAt).TotalHours > 72), // More than 72 hours
                    ReviewerWorkloads = reviewerWorkloads
                };

                return new PaginatedApplicationWithAssignmentDto
                {
                    PageNumber = pageNumber,
                    PageSize = pageSize,
                    TotalCount = totalCount,
                    TotalPages = totalPages,
                    HasPreviousPage = pageNumber > 1,
                    HasNextPage = pageNumber < totalPages,
                    Applications = applicationDtos,
                    Summary = summary
                };
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error getting applications for bulk reassignment: {ex.Message}");
                throw;
            }
        }

        #endregion
    }
}