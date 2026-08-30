using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using MMGT.API.DTOs.Application;
using MMGT.API.DTOs;
using MMGT.API.Services.Interfaces;

namespace MMGT.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class ApplicationsController : ControllerBase
    {
        private readonly IApplicationService _applicationService;
        private readonly IFileService _fileService;

        public ApplicationsController(IApplicationService applicationService, IFileService fileService)
        {
            _applicationService = applicationService;
            _fileService = fileService;
        }

        [HttpGet]
        [Authorize(Roles = "SUPER_ADMIN,ADMIN,LEAD_REVIEWER,SERVICE_MANAGER")]
        public async Task<ActionResult<PaginatedResponse<ApplicationDto>>> GetAllApplications([FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 50)
        {
            try
            {
                var pagination = new PaginationRequest { PageNumber = pageNumber, PageSize = pageSize };
                var result = await _applicationService.GetAllApplicationsPagedAsync(pagination);
                return Ok(result);
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }


        [HttpGet("my-applications")]
        [Authorize(Roles = "CLIENT,PMI")]
        public async Task<ActionResult<List<ApplicationDto>>> GetMyApplications()
        {
            try
            {
                var userId = GetCurrentUserId();
                var applications = await _applicationService.GetApplicationsByUserAsync(userId);
                return Ok(applications);
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [HttpGet("my-applications/paged")]
        [Authorize(Roles = "CLIENT,PMI,SERVICE_MANAGER")]
        public async Task<ActionResult<PaginatedResponse<ApplicationDto>>> GetMyApplicationsPaged([FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 50, [FromQuery] string? searchTerm = null)
        {
            try
            {
                var userId = GetCurrentUserId();
                var pagination = new PaginationRequest { PageNumber = pageNumber, PageSize = pageSize };
                var result = await _applicationService.GetApplicationsByUserPagedAsync(userId, pagination, searchTerm);
                return Ok(result);
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [HttpGet("my-applications/summary")]
        [Authorize(Roles = "CLIENT,PMI,SERVICE_MANAGER")]
        public async Task<ActionResult<ClientApplicationSummaryDto>> GetMyApplicationsSummary()
        {
            try
            {
                var userId = GetCurrentUserId();
                var result = await _applicationService.GetApplicationsSummaryByUserAsync(userId);
                return Ok(result);
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [HttpGet("my-applications/rejected")]
        [Authorize(Roles = "CLIENT,PMI,SERVICE_MANAGER")]
        public async Task<ActionResult<PaginatedResponse<ApplicationDetailsDto>>> GetMyRejectedApplications([FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 50)
        {
            try
            {
                var userId = GetCurrentUserId();
                var pagination = new PaginationRequest { PageNumber = pageNumber, PageSize = pageSize };
                var result = await _applicationService.GetApplicationsByUserRejectedPagedAsync(userId, pagination);
                return Ok(result);
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<ApplicationDto>> GetApplication(Guid id)
        {
            try
            {
                var application = await _applicationService.GetApplicationByIdAsync(id);
                return Ok(application);
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>
        /// Get complete application details with review history, assignments, and documents.
        /// SERVICE_MANAGER can view any application.
        /// LEAD_REVIEWER can view any application (for reassignment purposes).
        /// ADMIN/REVIEWER can view only assigned applications.
        /// Other authenticated users can view only their own applications.
        /// </summary>
        [HttpGet("{id}/details")]
        [Authorize]
        public async Task<ActionResult<ApplicationDetailsDto>> GetApplicationDetails(Guid id)
        {
            try
            {
                var userId = GetCurrentUserId();
                var application = await _applicationService.GetApplicationDetailsAsync(id);

                // Check authorization
                var userRole = User.FindFirst("http://schemas.microsoft.com/ws/2008/06/identity/claims/role")?.Value ?? string.Empty;

                // SERVICE_MANAGER can view any application
                if (userRole == "SERVICE_MANAGER")
                {
                    return Ok(application);
                }

                // Application owner can view their own
                if (application.UserId == userId)
                {
                    return Ok(application);
                }

                // LEAD_REVIEWER can view any application (for reassignment purposes)
                if (userRole == "LEAD_REVIEWER")
                {
                    return Ok(application);
                }

                // ADMIN/REVIEWER can view applications assigned to them
                if (userRole == "ADMIN" || userRole == "REVIEWER")
                {
                    // Check if user has an active assignment
                    var hasActiveAssignment = application.AssignmentHistory != null && 
                        application.AssignmentHistory.Any(a => a.IsActive);

                    if (hasActiveAssignment)
                    {
                        return Ok(application);
                    }
                }

                return Forbid();
            }
            catch (ApplicationException ex)
            {
                return NotFound(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        // ===== NEW: Check if RsaPin already exists =====
        [HttpGet("check-rsapin/{rsaPin}")]
        [Authorize(Roles = "CLIENT,PMI,SERVICE_MANAGER")]
        public async Task<ActionResult<object>> CheckRsaPin(string rsaPin)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(rsaPin))
                    return BadRequest(new { message = "RsaPin cannot be empty" });

                var exists = await _applicationService.CheckRsaPinExistsAsync(rsaPin);

                if (exists)
                {
                    var existing = await _applicationService.GetApplicationByRsaPinAsync(rsaPin);
                    return Ok(new
                    {
                        exists = true,
                        message = "An active application already exists with this RSA PIN",
                        applicationId = existing?.Id,
                        applicationNumber = existing?.ApplicationNumber,
                        status = existing?.Status,
                        borrowerName = $"{existing?.FirstName} {existing?.LastName}".Trim()
                    });
                }

                return Ok(new
                {
                    exists = false,
                    message = "No active application found with this RSA PIN. You can proceed."
                });
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }
        // ===== END NEW ENDPOINT =====

        [HttpPost]
        [Authorize(Roles = "CLIENT,PMI,SERVICE_MANAGER")]
        public async Task<ActionResult<ApplicationDto>> CreateApplication([FromBody] CreateApplicationRequest request)
        {
            try
            {
                var userId = GetCurrentUserId();
                var application = await _applicationService.CreateApplicationAsync(userId, request);
                return CreatedAtAction(nameof(GetApplication), new { id = application.Id }, application);
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [HttpPut("{id}")]
        [Authorize(Roles = "CLIENT,PMI,SERVICE_MANAGER")]
        public async Task<ActionResult<ApplicationDto>> UpdateApplication(Guid id, [FromBody] UpdateApplicationRequest request)
        {
            try
            {
                if (!ModelState.IsValid)
                    return BadRequest(ModelState);

                var userId = GetCurrentUserId();
                var updated = await _applicationService.UpdateApplicationAsync(id, userId, request);
                return Ok(updated);
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [HttpPost("{id}/submit")]
        [Authorize(Roles = "CLIENT,PMI,SERVICE_MANAGER")]
        public async Task<ActionResult> SubmitApplication(Guid id)
        {
            try
            {
                var result = await _applicationService.SubmitApplicationAsync(id);
                if (result)
                    return Ok(new { message = "Application submitted successfully" });
                else
                    return NotFound(new { message = "Application not found" });
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>
        /// Update application status by reviewer/admin.
        /// Valid statuses: draft, submitted, under_review, pending, approved, rejected, returned, completed.
        /// Pending status means "waiting to confirm information after reviewing".
        /// </summary>
        [HttpPut("{id}/status")]
        [Authorize(Roles = "SUPER_ADMIN,ADMIN")]
        public async Task<ActionResult<ApplicationDto>> UpdateApplicationStatus(Guid id, [FromBody] UpdateApplicationStatusRequest request)
        {
            try
            {
                var reviewerId = GetCurrentUserId();
                var application = await _applicationService.UpdateApplicationStatusAsync(id, reviewerId, request);
                return Ok(application);
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [HttpDelete("{id}")]
        [Authorize(Roles = "CLIENT")]
        public async Task<ActionResult> DeleteApplication(Guid id)
        {
            try
            {
                var result = await _applicationService.DeleteApplicationAsync(id);
                if (result)
                    return Ok(new { message = "Application deleted successfully" });
                else
                    return NotFound(new { message = "Application not found" });
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [HttpGet("{id}/review-history")]
        public async Task<ActionResult<List<ReviewHistoryDto>>> GetApplicationReviewHistory(Guid id)
        {
            try
            {
                var reviewHistory = await _applicationService.GetApplicationReviewHistoryAsync(id);
                return Ok(reviewHistory);
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [HttpGet("{id}/review-history/paged")]
        public async Task<ActionResult<PaginatedResponse<ReviewHistoryDto>>> GetApplicationReviewHistoryPaged(Guid id, [FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 50)
        {
            try
            {
                var pagination = new PaginationRequest { PageNumber = pageNumber, PageSize = pageSize };
                var reviewHistory = await _applicationService.GetApplicationReviewHistoryPagedAsync(id, pagination);
                return Ok(reviewHistory);
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [HttpGet("{id}/assignment-history")]
        [Authorize(Roles = "SUPER_ADMIN,ADMIN,LEAD_REVIEWER,REVIEWER")]
        public async Task<ActionResult<List<DTOs.Assignment.AssignmentHistoryDto>>> GetApplicationAssignmentHistory(Guid id)
        {
            try
            {
                var assignmentHistory = await _applicationService.GetApplicationAssignmentHistoryAsync(id);
                return Ok(assignmentHistory);
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [HttpGet("{id}/assignment-history/paged")]
        [Authorize(Roles = "SUPER_ADMIN,ADMIN,LEAD_REVIEWER,REVIEWER")]
        public async Task<ActionResult<PaginatedResponse<DTOs.Assignment.AssignmentHistoryDto>>> GetApplicationAssignmentHistoryPaged(Guid id, [FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 50)
        {
            try
            {
                var pagination = new PaginationRequest { PageNumber = pageNumber, PageSize = pageSize };
                var assignmentHistory = await _applicationService.GetApplicationAssignmentHistoryPagedAsync(id, pagination);
                return Ok(assignmentHistory);
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [HttpGet("{id}/documents")]
        public async Task<ActionResult<List<object>>> GetApplicationDocuments(Guid id)
        {
            try
            {
                // Verify application exists
                var application = await _applicationService.GetApplicationByIdAsync(id);

                // Get files
                var files = await _fileService.GetFilesByApplicationAsync(id);
                return Ok(files);
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>
        /// Download all BLOB documents for an application as a single merged PDF file.
        /// Only includes documents stored in the new system (BLOB storage).
        /// Legacy disk-stored documents are not included in the merged download.
        /// </summary>
        [HttpGet("{id}/documents/download-all")]
        public async Task<IActionResult> DownloadMergedDocuments(Guid id)
        {
            try
            {
                // Verify application exists and user has access
                var application = await _applicationService.GetApplicationByIdAsync(id);

                // Authorization: Verify user can access this application
                var userId = GetCurrentUserId();

                // Extract role from multiple possible claim names for compatibility
                var userRole = User.FindFirst("role")?.Value              // lowercase (common format)
                            ?? User.FindFirst("UserRole")?.Value           // camelCase (your token format)
                            ?? User.FindFirst("Role")?.Value               // Capital R (other systems)
                            ?? User.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value  // Standard claim
                            ?? string.Empty;

                // Owner or admin/reviewer can access
                bool hasAccess = application.UserId == userId || 
                                userRole.Contains("ADMIN") || 
                                userRole.Contains("REVIEWER") || 
                                userRole.Contains("LEAD_REVIEWER") ||
                                userRole.Contains("SERVICE_MANAGER");

                if (!hasAccess)
                    return Forbid();

                // Get merged PDF
                var mergedPdf = await _fileService.MergeBlobDocumentsAsync(id);

                // Generate filename
                var fileName = $"Application_{application.ApplicationNumber}_Documents_{DateTime.UtcNow:yyyy-MM-dd_HHmmss}.pdf";

                return File(mergedPdf, "application/pdf", fileName);
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [HttpGet("search")]
        [Authorize(Roles = "SUPER_ADMIN,ADMIN,SERVICE_MANAGER")]
        public async Task<ActionResult<PaginatedResponse<ApplicationSearchDto>>> SearchApplications(
            [FromQuery] string? rsaPin = null,
            [FromQuery] string? borrowerName = null,
            [FromQuery] DateTime? submittedFromDate = null,
            [FromQuery] DateTime? submittedToDate = null,
            [FromQuery] int pageNumber = 1,
            [FromQuery] int pageSize = 50)
        {
            try
            {
                var results = await _applicationService.SearchApplicationsAdvancedAsync(
                    rsaPin,
                    borrowerName,
                    submittedFromDate,
                    submittedToDate,
                    pageNumber,
                    pageSize);

                return Ok(results);
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [HttpGet("legacy-search")]
        public async Task<ActionResult<List<ApplicationDto>>> SearchApplications([FromQuery] string searchTerm)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(searchTerm))
                    return BadRequest(new { message = "Search term cannot be empty" });

                var results = await _applicationService.SearchApplicationsAsync(searchTerm);
                return Ok(results);
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

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
}