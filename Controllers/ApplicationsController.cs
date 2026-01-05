using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using MMGT.API.DTOs.Application;
using MMGT.API.Services.Interfaces;

namespace MMGT.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class ApplicationsController : ControllerBase
    {
        private readonly IApplicationService _applicationService;

        public ApplicationsController(IApplicationService applicationService)
        {
            _applicationService = applicationService;
        }

        [HttpGet]
        [Authorize(Roles = "SUPER_ADMIN,ADMIN")]
        public async Task<ActionResult<List<ApplicationDto>>> GetAllApplications()
        {
            try
            {
                var applications = await _applicationService.GetAllApplicationsAsync();
                return Ok(applications);
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [HttpGet("my-applications")]
        [Authorize(Roles = "CLIENT")]
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

        [HttpPost]
        [Authorize(Roles = "CLIENT")]
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
        [Authorize(Roles = "CLIENT")]
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
        [Authorize(Roles = "CLIENT")]
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