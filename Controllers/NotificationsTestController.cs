using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MMGT.API.Services;

namespace MMGT.API.Controllers
{
    public class EmailTestRequest
    {
        public string ToEmail { get; set; } = string.Empty;
    }

    /// <summary>Sends one test email so Infobip can be checked on its own. SUPER_ADMIN only.</summary>
    [ApiController]
    [Route("api/notifications-test")]
    [Authorize(Roles = "SUPER_ADMIN")]
    public class NotificationsTestController : ControllerBase
    {
        private readonly IStepDownNotifier _notifier;

        public NotificationsTestController(IStepDownNotifier notifier)
        {
            _notifier = notifier;
        }

        [HttpPost("email")]
        public async Task<IActionResult> Email([FromBody] EmailTestRequest request)
        {
            var to = (request?.ToEmail ?? string.Empty).Trim();
            if (to.Length == 0 || !to.Contains('@'))
                return BadRequest(new { message = "Enter a valid email address." });
            return Ok(await _notifier.SendTestAsync(to));
        }
    }
}
