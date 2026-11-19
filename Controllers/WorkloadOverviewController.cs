using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MMGT.API.Data;

namespace MMGT.API.Controllers
{
    /// <summary>Workload overview counts for the Lead Reviewer. Read-only, three COUNT queries.</summary>
    [ApiController]
    [Route("api/workload")]
    [Authorize(Roles = "LEAD_REVIEWER,SUPER_ADMIN")]
    public class WorkloadOverviewController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        public WorkloadOverviewController(ApplicationDbContext context) { _context = context; }

        /// <summary>
        /// Open = submitted, under_review, pending, returned or resubmitted, not archived, not locked.
        /// unassignedOpen = open applications with no active reviewer (what "waiting for an owner" should show).
        /// </summary>
        [HttpGet("overview")]
        public async Task<IActionResult> Overview()
        {
            var open = _context.Applications.AsNoTracking().Where(a => !a.IsLocked &&
                (a.Status == "submitted" || a.Status == "under_review" || a.Status == "pending" || a.Status == "returned" || a.Status == "resubmitted"));
            var openTotal = await open.CountAsync();
            var unassignedOpen = await open.CountAsync(a => !_context.ApplicationAssignments.Any(s => s.ApplicationId == a.Id && s.IsActive));
            return Ok(new { openTotal, unassignedOpen, assignedOpen = openTotal - unassignedOpen });
        }
    }
}