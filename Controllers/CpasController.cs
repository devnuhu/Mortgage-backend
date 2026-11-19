using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MMGT.API.Data;
using MMGT.API.Services;

namespace MMGT.API.Controllers
{
    /// <summary>CPAS information for an application, fetched by the API so browsers never call CPAS.</summary>
    [ApiController]
    [Route("api/cpas")]
    [Authorize(Roles = "SERVICE_MANAGER")]
    public class CpasController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly ICpasCaseStatusService _cases;
        public CpasController(ApplicationDbContext context, ICpasCaseStatusService cases) { _context = context; _cases = cases; }

        [HttpGet("case-status/{applicationId:guid}")]
        public async Task<IActionResult> CaseStatus(Guid applicationId)
        {
            var pin = await _context.Applications.AsNoTracking().Where(a => a.Id == applicationId).Select(a => a.RsaPin).FirstOrDefaultAsync();
            if (pin == null) return NotFound(new { message = "Application not found" });
            try
            {
                var status = await _cases.GetCurrentStatusAsync(pin, HttpContext.RequestAborted);
                return Ok(new { currentStatus = status, available = true });
            }
            catch (HttpRequestException)
            {
                return Ok(new { currentStatus = (string?)null, available = false });
            }
            catch (TaskCanceledException)
            {
                return Ok(new { currentStatus = (string?)null, available = false });
            }
        }
    }
}