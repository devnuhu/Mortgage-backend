using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MMGT.API.DTOs.Review;
using MMGT.API.Services;

namespace MMGT.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class ReviewController : ControllerBase
    {
        private readonly IDocumentReviewService _reviewService;

        public ReviewController(IDocumentReviewService reviewService)
        {
            _reviewService = reviewService;
        }

        [HttpPost("approve/{documentId}")]
        [Authorize(Roles = "REVIEWER,LEAD_REVIEWER,ADMIN")]
        public async Task<ActionResult> Approve(Guid documentId, [FromBody] ReviewRequest req)
        {
            try
            {
                var userId = GetCurrentUserId();
                var doc = await _reviewService.ApproveDocumentAsync(documentId, userId, req.Comments);
                return Ok(new { message = "Document approved", documentId = doc.Id });
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [HttpPost("reject/{documentId}")]
        [Authorize(Roles = "REVIEWER,LEAD_REVIEWER,ADMIN")]
        public async Task<ActionResult> Reject(Guid documentId, [FromBody] ReviewRequest req)
        {
            try
            {
                var userId = GetCurrentUserId();
                var doc = await _reviewService.RejectDocumentAsync(documentId, userId, req.Comments);
                return Ok(new { message = "Document rejected", documentId = doc.Id });
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [HttpPost("request-revision/{documentId}")]
        [Authorize(Roles = "REVIEWER,LEAD_REVIEWER,ADMIN")]
        public async Task<ActionResult> RequestRevision(Guid documentId, [FromBody] ReviewRequest req)
        {
            try
            {
                var userId = GetCurrentUserId();
                var doc = await _reviewService.RequestRevisionAsync(documentId, userId, req.Comments);
                return Ok(new { message = "Revision requested", documentId = doc.Id });
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
                throw new Exception("Invalid user token");
            return userId;
        }
    }
}
