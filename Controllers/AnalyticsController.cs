using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MMGT.API.Data;
using MMGT.API.DTOs.Performance;
using MMGT.API.DTOs.Escalation;
using MMGT.API.DTOs.Availability;
using MMGT.API.DTOs.Workload;
using MMGT.API.DTOs.Assignment;
using MMGT.API.Services;
using MMGT.API.Services.Interfaces;

namespace MMGT.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AnalyticsController : ControllerBase
    {
        private readonly IReviewerPerformanceService _performanceService;
        private readonly IEscalationService _escalationService;
        private readonly IReviewerAvailabilityService _reviewerAvailabilityService;
        private readonly IWorkloadSuggestionService _workloadSuggestionService;
        private readonly ICriteriaBasedReassignmentService _criteriaBasedReassignmentService;
        private readonly ApplicationDbContext _context;
        private readonly ILogger<AnalyticsController> _logger;

        public AnalyticsController(
            IReviewerPerformanceService performanceService,
            IEscalationService escalationService,
            IReviewerAvailabilityService reviewerAvailabilityService,
            IWorkloadSuggestionService workloadSuggestionService,
            ICriteriaBasedReassignmentService criteriaBasedReassignmentService,
            ApplicationDbContext context,
            ILogger<AnalyticsController> logger)
        {
            _performanceService = performanceService;
            _escalationService = escalationService;
            _reviewerAvailabilityService = reviewerAvailabilityService;
            _workloadSuggestionService = workloadSuggestionService;
            _criteriaBasedReassignmentService = criteriaBasedReassignmentService;
            _context = context;
            _logger = logger;
        }

        #region Performance Metrics

        /// <summary>
        /// Get performance metrics for a specific reviewer
        /// </summary>
        [HttpGet("reviewer/{reviewerId}/performance")]
        [Authorize(Roles = "LEAD_REVIEWER,SUPER_ADMIN,ADMIN")]
        public async Task<ActionResult<ReviewerPerformanceMetricsDto>> GetReviewerPerformance(Guid reviewerId)
        {
            try
            {
                var metrics = await _performanceService.GetReviewerMetricsAsync(reviewerId);
                return Ok(metrics);
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>
        /// Get performance metrics for all reviewers
        /// </summary>
        [HttpGet("performance/all")]
        [Authorize(Roles = "LEAD_REVIEWER,SUPER_ADMIN,ADMIN")]
        public async Task<ActionResult<List<ReviewerPerformanceMetricsDto>>> GetAllReviewersPerformance()
        {
            try
            {
                var metrics = await _performanceService.GetAllReviewersMetricsAsync();
                return Ok(metrics);
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>
        /// Get top performing reviewers
        /// </summary>
        [HttpGet("performance/top")]
        [Authorize(Roles = "LEAD_REVIEWER,SUPER_ADMIN,ADMIN")]
        public async Task<ActionResult<List<ReviewerPerformanceMetricsDto>>> GetTopPerformers([FromQuery] int count = 5)
        {
            try
            {
                var metrics = await _performanceService.GetTopPerformersAsync(count);
                return Ok(metrics);
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>
        /// Get bottom performing reviewers
        /// </summary>
        [HttpGet("performance/bottom")]
        [Authorize(Roles = "LEAD_REVIEWER,SUPER_ADMIN,ADMIN")]
        public async Task<ActionResult<List<ReviewerPerformanceMetricsDto>>> GetBottomPerformers([FromQuery] int count = 5)
        {
            try
            {
                var metrics = await _performanceService.GetBottomPerformersAsync(count);
                return Ok(metrics);
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>
        /// Get average turnaround time for a reviewer (last N days)
        /// </summary>
        [HttpGet("reviewer/{reviewerId}/turnaround-time")]
        [Authorize(Roles = "LEAD_REVIEWER,SUPER_ADMIN,ADMIN")]
        public async Task<ActionResult<object>> GetReviewerTurnaroundTime(Guid reviewerId, [FromQuery] int days = 30)
        {
            try
            {
                var avgTurnaroundHours = await _performanceService.GetAverageTurnaroundTimeAsync(reviewerId, days);
                return Ok(new
                {
                    reviewerId,
                    days,
                    averageTurnaroundTimeHours = Math.Round(avgTurnaroundHours, 2),
                    averageTurnaroundTimeDays = Math.Round(avgTurnaroundHours / 24, 2)
                });
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>
        /// Recalculate performance metrics for a specific reviewer
        /// </summary>
        [HttpPost("reviewer/{reviewerId}/recalculate-metrics")]
        [Authorize(Roles = "SUPER_ADMIN,LEAD_REVIEWER")]
        public async Task<ActionResult> RecalculateReviewerMetrics(Guid reviewerId)
        {
            try
            {
                await _performanceService.RecalculateMetricsAsync(reviewerId);
                return Ok(new { message = "Metrics recalculated successfully" });
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>
        /// Recalculate metrics for all reviewers
        /// </summary>
        [HttpPost("performance/recalculate-all")]
        [Authorize(Roles = "SUPER_ADMIN,LEAD_REVIEWER")]
        public async Task<ActionResult> RecalculateAllMetrics()
        {
            try
            {
                await _performanceService.RecalculateAllMetricsAsync();
                return Ok(new { message = "Metrics for all reviewers recalculated successfully" });
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        #endregion

        #region Workload Distribution

        /// <summary>
        /// Get workload distribution across all reviewers (summary view)
        /// </summary>
        [HttpGet("workload/distribution")]
        [Authorize(Roles = "LEAD_REVIEWER,SUPER_ADMIN,ADMIN")]
        public async Task<ActionResult<WorkloadDistributionSummaryDto>> GetWorkloadDistribution()
        {
            try
            {
                var distribution = await _performanceService.GetWorkloadDistributionAsync();
                return Ok(distribution);
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>
        /// Get workload distribution with pagination and sorting
        /// Supports sorting by: reviewername, workload, activeassignments, approvalrate, turnaroundtime, performancerating
        /// </summary>
        [HttpGet("reviewers/workload")]
        [Authorize(Roles = "LEAD_REVIEWER,SUPER_ADMIN,ADMIN")]
        public async Task<ActionResult<PaginatedWorkloadDistributionDto>> GetReviewersWorkloadPaged(
            [FromQuery] int pageNumber = 1,
            [FromQuery] int pageSize = 25,
            [FromQuery] string sortBy = "reviewername",
            [FromQuery] bool sortDescending = false)
        {
            try
            {
                var distribution = await _performanceService.GetWorkloadDistributionPagedAsync(
                    pageNumber,
                    pageSize,
                    sortBy,
                    sortDescending);
                return Ok(distribution);
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        #endregion

        #region Escalation Management

        /// <summary>
        /// Get all escalation rules
        /// </summary>
        [HttpGet("escalation-rules")]
        [Authorize(Roles = "SUPER_ADMIN,LEAD_REVIEWER")]
        public async Task<ActionResult<List<EscalationRuleDto>>> GetEscalationRules()
        {
            try
            {
                var rules = await _escalationService.GetAllEscalationRulesAsync();
                return Ok(rules);
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>
        /// Create a new escalation rule
        /// </summary>
        [HttpPost("escalation-rules")]
        [Authorize(Roles = "SUPER_ADMIN,LEAD_REVIEWER")]
        public async Task<ActionResult<EscalationRuleDto>> CreateEscalationRule([FromBody] CreateEscalationRuleRequest request)
        {
            try
            {
                var userId = GetCurrentUserId();
                var rule = await _escalationService.CreateEscalationRuleAsync(request, userId);
                return CreatedAtAction(nameof(GetEscalationRule), new { ruleId = rule.Id }, rule);
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>
        /// Get a specific escalation rule
        /// </summary>
        [HttpGet("escalation-rules/{ruleId}")]
        [Authorize(Roles = "SUPER_ADMIN,LEAD_REVIEWER")]
        public async Task<ActionResult<EscalationRuleDto>> GetEscalationRule(Guid ruleId)
        {
            try
            {
                var rule = await _escalationService.GetEscalationRuleAsync(ruleId);
                return Ok(rule);
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>
        /// Update an escalation rule
        /// </summary>
        [HttpPut("escalation-rules/{ruleId}")]
        [Authorize(Roles = "SUPER_ADMIN,LEAD_REVIEWER")]
        public async Task<ActionResult<EscalationRuleDto>> UpdateEscalationRule(Guid ruleId, [FromBody] UpdateEscalationRuleRequest request)
        {
            try
            {
                var userId = GetCurrentUserId();
                var rule = await _escalationService.UpdateEscalationRuleAsync(ruleId, request, userId);
                return Ok(rule);
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>
        /// Delete an escalation rule
        /// </summary>
        [HttpDelete("escalation-rules/{ruleId}")]
        [Authorize(Roles = "SUPER_ADMIN,LEAD_REVIEWER")]
        public async Task<ActionResult> DeleteEscalationRule(Guid ruleId)
        {
            try
            {
                var result = await _escalationService.DeleteEscalationRuleAsync(ruleId);
                if (!result)
                    return NotFound(new { message = "Escalation rule not found" });

                return Ok(new { message = "Escalation rule deleted successfully" });
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>
        /// Toggle escalation rule active status
        /// </summary>
        [HttpPatch("escalation-rules/{ruleId}/toggle")]
        [Authorize(Roles = "SUPER_ADMIN,LEAD_REVIEWER")]
        public async Task<ActionResult> ToggleEscalationRule(Guid ruleId, [FromBody] dynamic body)
        {
            try
            {
                // Extract isActive from body
                bool isActive = body?.isActive ?? true;

                var result = await _escalationService.ToggleEscalationRuleAsync(ruleId, isActive);
                if (!result)
                    return NotFound(new { message = "Escalation rule not found" });

                return Ok(new { message = "Escalation rule status updated successfully", isActive });
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>
        /// Check and escalate delayed assignments (manually trigger escalation check)
        /// </summary>
        [HttpPost("escalation/check-delays")]
        [Authorize(Roles = "SUPER_ADMIN,LEAD_REVIEWER")]
        public async Task<ActionResult> CheckAndEscalateDelayedAssignments()
        {
            try
            {
                await _escalationService.CheckAndEscalateDelayedAssignmentsAsync();
                return Ok(new { message = "Escalation check completed" });
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>
        /// Get all active escalations
        /// </summary>
        [HttpGet("escalations/active")]
        [Authorize(Roles = "SUPER_ADMIN,LEAD_REVIEWER,ADMIN")]
        public async Task<ActionResult<List<AssignmentEscalationDto>>> GetActiveEscalations()
        {
            try
            {
                var escalations = await _escalationService.GetActiveEscalationsAsync();
                return Ok(escalations);
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>
        /// Get escalations for a specific reviewer
        /// </summary>
        [HttpGet("escalations/reviewer/{reviewerId}")]
        [Authorize(Roles = "SUPER_ADMIN,LEAD_REVIEWER,ADMIN")]
        public async Task<ActionResult<List<AssignmentEscalationDto>>> GetReviewerEscalations(Guid reviewerId)
        {
            try
            {
                var escalations = await _escalationService.GetEscalationsForReviewerAsync(reviewerId);
                return Ok(escalations);
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>
        /// Resolve an escalation
        /// </summary>
        [HttpPut("escalations/{escalationId}/resolve")]
        [Authorize(Roles = "SUPER_ADMIN,LEAD_REVIEWER")]
        public async Task<ActionResult> ResolveEscalation(Guid escalationId, [FromBody] ResolveEscalationRequest request)
        {
            try
            {
                var userId = GetCurrentUserId();
                var result = await _escalationService.ResolveEscalationAsync(escalationId, request.ResolutionComments, userId);
                if (!result)
                    return NotFound(new { message = "Escalation not found" });

                return Ok(new { message = "Escalation resolved successfully" });
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        #endregion

        #region Reassignment Management

        /// <summary>
        /// Get all assignments for a specific reviewer
        /// </summary>
        [HttpGet("reviewer/{reviewerId}/assignments")]
        [Authorize(Roles = "LEAD_REVIEWER,SUPER_ADMIN")]
        public async Task<ActionResult<List<ApplicationAssignmentDto>>> GetReviewerAssignments(Guid reviewerId)
        {
            try
            {
                var assignments = await _escalationService.GetAssignmentsForReviewerAsync(reviewerId);
                var dto = assignments.Select(a => new ApplicationAssignmentDto
                {
                    Id = a.Id,
                    ApplicationId = a.ApplicationId,
                    ApplicationNumber = a.Application?.ApplicationNumber ?? string.Empty,
                    ReviewerId = a.ReviewerId,
                    ReviewerName = a.Reviewer != null ? $"{a.Reviewer.FirstName} {a.Reviewer.LastName}" : "Unknown",
                    ReviewerEmail = a.Reviewer?.Email ?? string.Empty,
                    AssignedAt = a.AssignedAt,
                    IsActive = a.IsActive,
                    ReassignmentReason = a.ReassignmentReason,
                    AssignedBy = a.AssignedBy
                }).ToList();

                return Ok(dto);
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>
        /// Reassign a single application to a different reviewer
        /// </summary>
        [HttpPost("assignments/{assignmentId}/reassign")]
        [Authorize(Roles = "LEAD_REVIEWER,SUPER_ADMIN")]
        public async Task<ActionResult> ReassignAssignment(Guid assignmentId, [FromBody] ReassignmentRequest request)
        {
            try
            {
                if (request.NewReviewerId == Guid.Empty)
                    return BadRequest(new { message = "New reviewer ID is required" });

                if (string.IsNullOrEmpty(request.Reason))
                    return BadRequest(new { message = "Reassignment reason is required" });

                var result = await _escalationService.ReassignApplicationAsync(assignmentId, request.NewReviewerId, request.Reason);

                if (!result)
                    return BadRequest(new { message = "Failed to reassign assignment. Verify the assignment and reviewer exist." });

                return Ok(new { message = "Application reassigned successfully" });
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>
        /// Reassign multiple applications to a different reviewer (bulk operation)
        /// </summary>
        [HttpPost("assignments/reassign-bulk")]
        [Authorize(Roles = "LEAD_REVIEWER,SUPER_ADMIN")]
        public async Task<ActionResult> ReassignMultipleAssignments([FromBody] BulkReassignmentRequest request)
        {
            try
            {
                if (!request.AssignmentIds.Any())
                    return BadRequest(new { message = "At least one assignment ID is required" });

                if (request.NewReviewerId == Guid.Empty)
                    return BadRequest(new { message = "New reviewer ID is required" });

                if (string.IsNullOrEmpty(request.Reason))
                    return BadRequest(new { message = "Reassignment reason is required" });

                var result = await _escalationService.ReassignMultipleApplicationsAsync(request.AssignmentIds, request.NewReviewerId, request.Reason);

                if (!result)
                    return BadRequest(new { message = "Failed to reassign applications. Verify the assignments and reviewer exist." });

                return Ok(new 
                { 
                    message = $"Successfully reassigned {request.AssignmentIds.Count} application(s) to the new reviewer",
                    reassignedCount = request.AssignmentIds.Count
                });
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>
        /// Get all applications with their current assignment details (for bulk reassignment)
        /// Lead Reviewers use this to see what Admin user has each application and for how long
        /// </summary>
        [HttpGet("applications/for-bulk-reassignment")]
        [Authorize(Roles = "LEAD_REVIEWER,SUPER_ADMIN,ADMIN")]
        public async Task<ActionResult<PaginatedApplicationWithAssignmentDto>> GetApplicationsForBulkReassignment(
            [FromQuery] int pageNumber = 1,
            [FromQuery] int pageSize = 25,
            [FromQuery] string? sortBy = "applicationNumber",
            [FromQuery] bool sortDescending = false,
            [FromQuery] Guid? reviewerId = null,
            [FromQuery] string? applicationStatus = null,
            [FromQuery] string? rsaPin = null,
            [FromQuery] string? applicationNumber = null,
            [FromQuery] DateTime? createdDateFrom = null,
            [FromQuery] DateTime? createdDateTo = null,
            [FromQuery] DateTime? submittedDateFrom = null,
            [FromQuery] DateTime? submittedDateTo = null)
        {
            try
            {
                var result = await _escalationService.GetApplicationsForBulkReassignmentAsync(
                    pageNumber,
                    pageSize,
                    sortBy,
                    sortDescending,
                    reviewerId,
                    applicationStatus,
                    rsaPin,
                    applicationNumber,
                    createdDateFrom,
                    createdDateTo,
                    submittedDateFrom,
                    submittedDateTo);
                return Ok(result);
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        #endregion

        #region Advanced Workload Management

        /// <summary>
        /// Get workload balancing suggestions (rebalancing recommendations)
        /// </summary>
        [HttpGet("workload/suggestions")]
        [Authorize(Roles = "LEAD_REVIEWER,SUPER_ADMIN,ADMIN")]
        public async Task<ActionResult> GetWorkloadBalancingSuggestions()
        {
            try
            {
                var suggestions = await _workloadSuggestionService.GetWorkloadBalancingSuggestionsAsync();
                return Ok(suggestions);
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>
        /// Get current availability status for a reviewer
        /// </summary>
        [HttpGet("reviewers/{reviewerId}/availability")]
        [Authorize(Roles = "LEAD_REVIEWER,SUPER_ADMIN,ADMIN,REVIEWER")]
        public async Task<ActionResult> GetReviewerAvailability(Guid reviewerId)
        {
            try
            {
                var availability = await _reviewerAvailabilityService.GetReviewerAvailabilityAsync(reviewerId);
                if (availability == null)
                    return NotFound(new { message = "Reviewer not found" });

                return Ok(availability);
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>
        /// Mark a reviewer as available for assignments
        /// </summary>
        [HttpPut("reviewers/{reviewerId}/availability/available")]
        [Authorize(Roles = "LEAD_REVIEWER,SUPER_ADMIN,ADMIN")]
        public async Task<ActionResult> MarkReviewerAvailable(Guid reviewerId)
        {
            try
            {
                var userId = GetCurrentUserId();
                var availability = await _reviewerAvailabilityService.MarkAvailableAsync(reviewerId, userId);
                return Ok(new { message = "Reviewer marked as available", availability });
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>
        /// Mark a reviewer as unavailable (with optional reason and end date)
        /// </summary>
        [HttpPut("reviewers/{reviewerId}/availability/unavailable")]
        [Authorize(Roles = "LEAD_REVIEWER,SUPER_ADMIN,ADMIN")]
        public async Task<ActionResult> MarkReviewerUnavailable(Guid reviewerId, [FromBody] MarkUnavailableRequest request)
        {
            try
            {
                _logger.LogInformation($"[MARK-UNAVAILABLE-ENDPOINT] Called for ReviewerId={reviewerId}");
                _logger.LogInformation($"[MARK-UNAVAILABLE-ENDPOINT] Request: Reason={request?.Reason}, EndDate={request?.EndDate}");

                var userId = GetCurrentUserId();
                _logger.LogInformation($"[MARK-UNAVAILABLE-ENDPOINT] Current UserId={userId}");

                // Default to 7 days from now if no end date provided
                var endDate = request?.EndDate ?? DateTime.UtcNow.AddDays(7);

                // Validate end date is in the future
                if (endDate <= DateTime.UtcNow)
                {
                    _logger.LogWarning($"[MARK-UNAVAILABLE-ENDPOINT] INVALID EndDate: {endDate} is not in the future (NOW={DateTime.UtcNow})");
                    return BadRequest(new { message = "End date must be in the future" });
                }

                var availability = await _reviewerAvailabilityService.MarkUnavailableAsync(
                    reviewerId, 
                    request?.Reason ?? string.Empty, 
                    endDate, 
                    userId);

                _logger.LogInformation($"[MARK-UNAVAILABLE-ENDPOINT] SUCCESS - Returning response with Status={availability.Status}");
                return Ok(new { message = "Reviewer marked as unavailable", availability });
            }
            catch (Exception ex)
            {
                _logger.LogError($"[MARK-UNAVAILABLE-ENDPOINT] ERROR: {ex.Message}");
                _logger.LogError($"[MARK-UNAVAILABLE-ENDPOINT] Exception: {ex}");
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>
        /// Mark a reviewer as on leave with end date
        /// </summary>
        [HttpPut("reviewers/{reviewerId}/availability/on-leave")]
        [Authorize(Roles = "LEAD_REVIEWER,SUPER_ADMIN,ADMIN")]
        public async Task<ActionResult> MarkReviewerOnLeave(Guid reviewerId, [FromBody] MarkOnLeaveRequest request)
        {
            try
            {
                if (request == null || request.EndDate == default)
                {
                    _logger.LogWarning($"[MARK-ON-LEAVE-ENDPOINT] EndDate is required for ReviewerId={reviewerId}");
                    return BadRequest(new { message = "End date is required" });
                }

                // Validate end date is in the future
                if (request.EndDate <= DateTime.UtcNow)
                {
                    _logger.LogWarning($"[MARK-ON-LEAVE-ENDPOINT] INVALID EndDate: {request.EndDate} is not in the future (NOW={DateTime.UtcNow})");
                    return BadRequest(new { message = "End date must be in the future" });
                }

                var userId = GetCurrentUserId();
                _logger.LogInformation($"[MARK-ON-LEAVE-ENDPOINT] Called for ReviewerId={reviewerId}, EndDate={request.EndDate}, Reason={request.Reason}");

                var availability = await _reviewerAvailabilityService.MarkOnLeaveAsync(
                    reviewerId, 
                    request.EndDate, 
                    request.Reason, 
                    userId);

                _logger.LogInformation($"[MARK-ON-LEAVE-ENDPOINT] SUCCESS - Reviewer marked on leave");
                return Ok(new { message = "Reviewer marked as on leave", availability });
            }
            catch (Exception ex)
            {
                _logger.LogError($"[MARK-ON-LEAVE-ENDPOINT] ERROR: {ex.Message}");
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>
        /// Get all currently unavailable reviewers with their active assignment counts
        /// Returns clean DTO without sensitive data (no password hashes or internal fields)
        /// </summary>
        [HttpGet("reviewers/availability/unavailable")]
        [Authorize(Roles = "LEAD_REVIEWER,SUPER_ADMIN,ADMIN")]
        public async Task<ActionResult> GetUnavailableReviewers()
        {
            try
            {
                _logger.LogInformation("[ANALYTICS-CONTROLLER] GetUnavailableReviewers called");

                var unavailableReviewers = await _reviewerAvailabilityService.GetUnavailableReviewersAsync();

                _logger.LogInformation($"[ANALYTICS-CONTROLLER] Service returned {unavailableReviewers.Count} unavailable reviewers");

                // Map to clean DTO and include active assignment counts
                var responseDto = new List<ReviewerAvailabilityResponseDto>();

                foreach (var availability in unavailableReviewers)
                {
                    _logger.LogInformation($"[ANALYTICS-CONTROLLER] Processing unavailable reviewer: {availability.Reviewer?.FirstName} {availability.Reviewer?.LastName}");

                    // Count active assignments for this reviewer
                    var activeAssignmentCount = await _context.ApplicationAssignments
                        .Where(a => a.ReviewerId == availability.ReviewerId && a.IsActive)
                        .CountAsync();

                    var dto = new ReviewerAvailabilityResponseDto
                    {
                        Id = availability.Id,
                        ReviewerId = availability.ReviewerId,
                        FirstName = availability.Reviewer?.FirstName ?? string.Empty,
                        LastName = availability.Reviewer?.LastName ?? string.Empty,
                        Email = availability.Reviewer?.Email ?? string.Empty,
                        Role = availability.Reviewer?.Role ?? string.Empty,
                        Status = availability.Status,
                        Reason = availability.Reason,
                        StatusStartDate = availability.StatusStartDate,
                        StatusEndDate = availability.StatusEndDate,
                        ActiveAssignmentCount = activeAssignmentCount
                    };
                    responseDto.Add(dto);
                    _logger.LogInformation($"[ANALYTICS-CONTROLLER] Added to response: {dto.FullName}");
                }

                _logger.LogInformation($"[ANALYTICS-CONTROLLER] Returning {responseDto.Count} DTOs");
                return Ok(responseDto);
            }
            catch (Exception ex)
            {
                _logger.LogError($"[ANALYTICS-CONTROLLER] Error in GetUnavailableReviewers: {ex.Message}");
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>
        /// Get all currently available reviewers with their active assignment counts
        /// Returns clean DTO without sensitive data (no password hashes or internal fields)
        /// </summary>
        [HttpGet("reviewers/availability/available")]
        [Authorize(Roles = "LEAD_REVIEWER,SUPER_ADMIN,ADMIN")]
        public async Task<ActionResult> GetAvailableReviewers()
        {
            try
            {
                var availableReviewers = await _reviewerAvailabilityService.GetAvailableReviewersAsync();

                // Map to clean DTO and include active assignment counts
                var responseDto = new List<ReviewerAvailabilityResponseDto>();

                foreach (var availability in availableReviewers)
                {
                    // Count active assignments for this reviewer
                    var activeAssignmentCount = await _context.ApplicationAssignments
                        .Where(a => a.ReviewerId == availability.ReviewerId && a.IsActive)
                        .CountAsync();

                    responseDto.Add(new ReviewerAvailabilityResponseDto
                    {
                        Id = availability.Id,
                        ReviewerId = availability.ReviewerId,
                        FirstName = availability.Reviewer?.FirstName ?? string.Empty,
                        LastName = availability.Reviewer?.LastName ?? string.Empty,
                        Email = availability.Reviewer?.Email ?? string.Empty,
                        Role = availability.Reviewer?.Role ?? string.Empty,
                        Status = availability.Status,
                        Reason = availability.Reason,
                        StatusStartDate = availability.StatusStartDate,
                        StatusEndDate = availability.StatusEndDate,
                        ActiveAssignmentCount = activeAssignmentCount
                    });
                }

                return Ok(responseDto);
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>
        /// Execute reassignments based on predefined criteria (auto-reassignment)
        /// </summary>
        [HttpPost("applications/reassign-by-criteria")]
        [Authorize(Roles = "ADMIN,SUPER_ADMIN")]
        public async Task<ActionResult> ReassignByCriteria([FromBody] ReassignmentCriteriaRequest request)
        {
            try
            {
                if (request == null)
                    return BadRequest(new { message = "Reassignment criteria is required" });

                var criteria = new ReassignmentCriteriaDto
                {
                    WorkloadThresholdPercentage = request.WorkloadThresholdPercentage,
                    ReassignIfUnavailable = request.ReassignIfUnavailable,
                    TargetWorkloadMaxPercentage = request.TargetWorkloadMaxPercentage,
                    PrioritizeHighPerformers = request.PrioritizeHighPerformers,
                    MinimumApprovalRatePercentage = request.MinimumApprovalRatePercentage,
                    MaxReassignments = request.MaxReassignments
                };

                var results = await _criteriaBasedReassignmentService.ExecuteReassignmentsByCriteriaAsync(criteria);
                return Ok(new 
                { 
                    message = $"Executed {results.Count} reassignment(s) based on criteria",
                    reassignments = results 
                });
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>
        /// Auto-balance workload across all reviewers
        /// </summary>
        [HttpPost("applications/auto-balance-workload")]
        [Authorize(Roles = "ADMIN,SUPER_ADMIN")]
        public async Task<ActionResult> AutoBalanceWorkload()
        {
            try
            {
                var results = await _criteriaBasedReassignmentService.AutoBalanceWorkloadAsync();
                return Ok(new 
                { 
                    message = $"Auto-balanced workload with {results.Count} reassignment(s)",
                    reassignments = results 
                });
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>
        /// Get reassignment history/audit logs (recent reassignments)
        /// </summary>
        [HttpGet("reassignments/history")]
        [Authorize(Roles = "LEAD_REVIEWER,SUPER_ADMIN,ADMIN")]
        public async Task<ActionResult> GetReassignmentHistory([FromQuery] int days = 30, [FromQuery] int pageSize = 100)
        {
            try
            {
                var history = await _performanceService.GetReassignmentHistoryAsync(days, pageSize);
                return Ok(history);
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>
        /// Get reassignment history for a specific reviewer
        /// </summary>
        [HttpGet("reassignments/history/reviewer/{reviewerId}")]
        [Authorize(Roles = "LEAD_REVIEWER,SUPER_ADMIN,ADMIN")]
        public async Task<ActionResult> GetReviewerReassignmentHistory(Guid reviewerId, [FromQuery] int days = 30)
        {
            try
            {
                var history = await _performanceService.GetReassignmentHistoryForReviewerAsync(reviewerId, days);
                return Ok(history);
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>
        /// Get reassignment metrics and audit statistics
        /// </summary>
        [HttpGet("reassignments/metrics")]
        [Authorize(Roles = "LEAD_REVIEWER,SUPER_ADMIN,ADMIN")]
        public async Task<ActionResult> GetReassignmentMetrics()
        {
            try
            {
                var metrics = await _performanceService.GetReassignmentMetricsAsync();
                return Ok(metrics);
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        #endregion

        #region Audit & Hours Tracking

        /// <summary>
        /// Get hours spent on a specific application by all assigned reviewers.
        /// Provides detailed audit trail of time investment per application.
        /// </summary>
        [HttpGet("applications/{applicationId}/hours-by-reviewer")]
        [Authorize(Roles = "LEAD_REVIEWER,SUPER_ADMIN,ADMIN")]
        public async Task<ActionResult<List<ApplicationHoursTrackingDto>>> GetApplicationHoursTracking(Guid applicationId)
        {
            try
            {
                var hoursData = await _performanceService.GetApplicationHoursTrackingAsync(applicationId);
                return Ok(hoursData);
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>
        /// Get comprehensive audit summary of hours spent by a reviewer over a date range.
        /// Includes per-application breakdown and aggregate statistics for compliance reporting.
        /// </summary>
        [HttpGet("audit/reviewer/{reviewerId}/hours-summary")]
        [Authorize(Roles = "LEAD_REVIEWER,SUPER_ADMIN")]
        public async Task<ActionResult<ReviewerHoursAuditSummaryDto>> GetReviewerHoursAuditSummary(
            Guid reviewerId,
            [FromQuery] DateTime? dateFrom = null,
            [FromQuery] DateTime? dateTo = null)
        {
            try
            {
                var auditSummary = await _performanceService.GetReviewerHoursAuditSummaryAsync(reviewerId, dateFrom, dateTo);
                return Ok(auditSummary);
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>
        /// Get summary of hours spent by a reviewer (without application details).
        /// Quick endpoint for dashboard display of total hours and statistics.
        /// </summary>
        [HttpGet("reviewer/{reviewerId}/hours-summary")]
        [Authorize(Roles = "LEAD_REVIEWER,SUPER_ADMIN,ADMIN")]
        public async Task<ActionResult<object>> GetReviewerHoursSummary(
            Guid reviewerId,
            [FromQuery] DateTime? dateFrom = null,
            [FromQuery] DateTime? dateTo = null)
        {
            try
            {
                var auditSummary = await _performanceService.GetReviewerHoursAuditSummaryAsync(reviewerId, dateFrom, dateTo);

                // Return summary without detailed application list for dashboard use
                return Ok(new
                {
                    reviewerId = auditSummary.ReviewerId,
                    reviewerName = auditSummary.ReviewerName,
                    reviewerEmail = auditSummary.ReviewerEmail,
                    dateFrom = auditSummary.DateFrom,
                    dateTo = auditSummary.DateTo,
                    totalHoursSpent = Math.Round(auditSummary.TotalHoursSpent, 2),
                    totalApplicationsProcessed = auditSummary.TotalApplicationsProcessed,
                    totalDocumentsReviewed = auditSummary.TotalDocumentsReviewed,
                    averageHoursPerApplication = Math.Round(auditSummary.AverageHoursPerApplication, 2),
                    averageHoursPerDocument = auditSummary.TotalDocumentsReviewed > 0 
                        ? Math.Round(auditSummary.TotalHoursSpent / auditSummary.TotalDocumentsReviewed, 2)
                        : 0
                });
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        #endregion

        private Guid GetCurrentUserId()
        {
            var userIdClaim = User.FindFirst("UserId");
            if (userIdClaim == null || !Guid.TryParse(userIdClaim.Value, out var userId))
            {
                throw new Exception("Invalid user token");
            }
            return userId;
        }
    }

    public class ResolveEscalationRequest
    {
        public string ResolutionComments { get; set; } = string.Empty;
    }

    public class ReassignmentRequest
    {
        public Guid NewReviewerId { get; set; }
        public string Reason { get; set; } = string.Empty;
    }

    public class BulkReassignmentRequest
    {
        public List<Guid> AssignmentIds { get; set; } = new();
        public Guid NewReviewerId { get; set; }
        public string Reason { get; set; } = string.Empty;
    }
}
