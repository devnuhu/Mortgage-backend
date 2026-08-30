using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MMGT.API.DTOs.Assignment;
using MMGT.API.Services;

namespace MMGT.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class AssignmentsController : ControllerBase
    {
        private readonly IReviewerAssignmentService _assignmentService;
        private readonly ILogger<AssignmentsController> _logger;

        public AssignmentsController(IReviewerAssignmentService assignmentService, ILogger<AssignmentsController> logger)
        {
            _assignmentService = assignmentService;
            _logger = logger;
        }

        [HttpPost("auto/{applicationId}")]
        [Authorize(Roles = "ADMIN,SUPER_ADMIN")]
        public async Task<ActionResult<AssignmentResponseDto>> AutoAssign(Guid applicationId)
        {
            try
            {
                var assignment = await _assignmentService.AutoAssignApplicationAsync(applicationId);
                var dto = new AssignmentResponseDto
                {
                    Id = assignment.Id,
                    ApplicationId = assignment.ApplicationId,
                    ReviewerId = assignment.ReviewerId,
                    AssignedBy = assignment.AssignedBy,
                    AssignedAt = assignment.AssignedAt,
                    IsActive = assignment.IsActive,
                    ReassignmentReason = assignment.ReassignmentReason
                };
                return Ok(dto);
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [HttpPost("assign/{applicationId}")]
        [Authorize(Roles = "LEAD_REVIEWER")]
        public async Task<ActionResult<AssignmentResponseDto>> Assign(Guid applicationId, [FromBody] ReassignRequest req)
        {
            try
            {
                var userId = GetCurrentUserId();
                var assignment = await _assignmentService.AssignApplicationAsync(applicationId, req.NewReviewerId, userId, req.Reason);
                var dto = new AssignmentResponseDto
                {
                    Id = assignment.Id,
                    ApplicationId = assignment.ApplicationId,
                    ReviewerId = assignment.ReviewerId,
                    AssignedBy = assignment.AssignedBy,
                    AssignedAt = assignment.AssignedAt,
                    IsActive = assignment.IsActive,
                    ReassignmentReason = assignment.ReassignmentReason
                };
                return Ok(dto);
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [HttpPost("reassign/{applicationId}")]
        [Authorize(Roles = "LEAD_REVIEWER")]
        public async Task<ActionResult<AssignmentResponseDto>> Reassign(Guid applicationId, [FromBody] ReassignRequest req)
        {
            try
            {
                var userId = GetCurrentUserId();
                var assignment = await _assignmentService.ManuallyReassignApplicationAsync(applicationId, req.NewReviewerId, userId, req.Reason);
                var dto = new AssignmentResponseDto
                {
                    Id = assignment.Id,
                    ApplicationId = assignment.ApplicationId,
                    ReviewerId = assignment.ReviewerId,
                    AssignedBy = assignment.AssignedBy,
                    AssignedAt = assignment.AssignedAt,
                    IsActive = assignment.IsActive,
                    ReassignmentReason = assignment.ReassignmentReason
                };
                return Ok(dto);
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [HttpGet("current/{applicationId}")]
        public async Task<ActionResult<AssignmentResponseDto>> GetCurrent(Guid applicationId)
        {
            var assignment = await _assignmentService.GetCurrentAssignmentAsync(applicationId);
            if (assignment == null) return NotFound();

            var dto = new AssignmentResponseDto
            {
                Id = assignment.Id,
                ApplicationId = assignment.ApplicationId,
                ReviewerId = assignment.ReviewerId,
                AssignedBy = assignment.AssignedBy,
                AssignedAt = assignment.AssignedAt,
                IsActive = assignment.IsActive,
                ReassignmentReason = assignment.ReassignmentReason
            };

            return Ok(dto);
        }

        [HttpGet("mine")]
        [Authorize(Roles = "REVIEWER,LEAD_REVIEWER, ADMIN")]
        public async Task<ActionResult<List<AssignmentResponseDto>>> MyAssignments()
        {
            var userId = GetCurrentUserId();
            var apps = await _assignmentService.GetReviewerAssignmentsAsync(userId);
            var list = apps.Select(a => new AssignmentResponseDto
            {
                Id = Guid.Empty, // assignment id omitted here; caller can use current/{applicationId}
                ApplicationId = a.Id,
                ReviewerId = userId,
                AssignedAt = DateTime.UtcNow,
                IsActive = true
            }).ToList();

            return Ok(list);
        }

        /// <summary>
        /// Retrieve unassigned applications with pagination, filtering, and sorting.
        /// Used to populate the bulk assignment grid on the Lead Reviewer dashboard.
        /// 
        /// Query Parameters:
        /// - pageNumber: page number (default 1)
        /// - pageSize: results per page (default 30, max 100)
        /// - sortBy: field to sort by ("applicationnumber", "applicantname", "status", "createdat", "submittedat")
        /// - sortDescending: sort order (default true)
        /// - applicationStatus: filter by status (partial match, case-insensitive)
        /// - rsaPin: filter by RSA PIN (partial match, case-insensitive)
        /// - borrowerName: filter by borrower name (partial match, case-insensitive)
        /// - pmiCompanyId: filter by PMI company GUID (exact match)
        /// - createdFromDate: filter applications created from date (ISO 8601 format)
        /// - createdToDate: filter applications created until date (ISO 8601 format)
        /// 
        /// Example: GET /api/Assignments/unassigned-for-bulk?pageNumber=1&pageSize=50&rsaPin=RSA&borrowerName=John&sortBy=createdat&sortDescending=true
        /// </summary>
        [HttpGet("unassigned-for-bulk")]
        [Authorize(Roles = "LEAD_REVIEWER,ADMIN")]
        public async Task<ActionResult<PaginatedApplicationWithAssignmentDto>> GetUnassignedApplicationsForBulkAssignment(
            [FromQuery] int pageNumber = 1,
            [FromQuery] int pageSize = 30,
            [FromQuery] string? sortBy = null,
            [FromQuery] bool sortDescending = true,
            [FromQuery] string? applicationStatus = null,
            [FromQuery] Guid? pmiCompanyId = null,
            [FromQuery] string? rsaPin = null,
            [FromQuery] string? borrowerName = null,
            [FromQuery] DateTime? createdFromDate = null,
            [FromQuery] DateTime? createdToDate = null)
        {
            try
            {
                var result = await _assignmentService.GetUnassignedApplicationsForBulkAssignmentAsync(
                    pageNumber,
                    pageSize,
                    sortBy,
                    sortDescending,
                    applicationStatus,
                    pmiCompanyId,
                    rsaPin,
                    borrowerName,
                    createdFromDate,
                    createdToDate);

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error retrieving unassigned applications for bulk assignment: {ex.Message}");
                return StatusCode(500, new { message = "Error retrieving unassigned applications" });
            }
        }

        /// <summary>
        /// Bulk assign multiple unassigned applications to a single reviewer.
        /// 
        /// Example request:
        /// POST /api/Assignments/bulk-assign
        /// {
        ///   "applicationIds": ["12345678-1234-1234-1234-123456789012", "..."],
        ///   "reviewerId": "87654321-4321-4321-4321-210987654321",
        ///   "reason": "Bulk migration assignment" (optional)
        /// }
        /// 
        /// Response:
        /// {
        ///   "successCount": 47,
        ///   "failureCount": 3,
        ///   "messages": ["Successfully assigned 47 applications", "..."],
        ///   "skippedApplicationIds": ["...", "...", "..."]
        /// }
        /// </summary>
        [HttpPost("bulk-assign")]
        [Authorize(Roles = "LEAD_REVIEWER,ADMIN")]
        public async Task<ActionResult<BulkAssignResponse>> BulkAssign(
            [FromBody] BulkAssignRequest request)
        {
            try
            {
                // Validate request structure
                if (request == null)
                    return BadRequest(new { message = "Request body is required" });

                if (request.ApplicationIds == null || !request.ApplicationIds.Any())
                    return BadRequest(new { message = "At least one application must be selected" });

                if (request.ReviewerId == Guid.Empty)
                    return BadRequest(new { message = "A valid reviewer must be selected" });

                // Get current user ID from JWT claim
                var currentUserId = GetCurrentUserId();
                if (currentUserId == Guid.Empty)
                    return Unauthorized(new { message = "Unable to identify current user" });

                // Call service
                var result = await _assignmentService.BulkAssignApplicationsAsync(
                    request.ApplicationIds,
                    request.ReviewerId,
                    currentUserId,
                    request.Reason);

                // Determine HTTP status based on outcome
                var statusCode = result.SuccessCount > 0 ? 200 : 400;
                return StatusCode(statusCode, result);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Internal server error occurred during bulk assignment" });
            }
        }

        private Guid GetCurrentUserId()
        {
            var userIdClaim = User.FindFirst("UserId");
            if (userIdClaim == null || !Guid.TryParse(userIdClaim.Value, out var userId))
                throw new Exception("Invalid user token");
            return userId;
        }
    }
}
