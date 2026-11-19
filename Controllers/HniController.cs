using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MMGT.API.Data;
using MMGT.API.Services;

namespace MMGT.API.Controllers
{
    /// <summary>HNI applications for the Lead Reviewer / Super Admin. Only a yes/no flag is ever returned, never a balance.</summary>
    [ApiController]
    [Route("api/hni")]
    [Authorize(Roles = "LEAD_REVIEWER,SUPER_ADMIN")]
    public class HniController : ControllerBase
    {
        private static readonly string[] Active = { "submitted", "under_review", "pending", "returned", "resubmitted" };
        private readonly ApplicationDbContext _context;
        private readonly IHniService _hni;
        public HniController(ApplicationDbContext context, IHniService hni) { _context = context; _hni = hni; }

        private Guid? CurrentUserId()
        {
            var c = User.FindFirst("UserId");
            return c != null && Guid.TryParse(c.Value, out var id) ? id : null;
        }

        /// <summary>All HNI applications with status, date created and the assigned reviewer.</summary>
        [HttpGet("applications")]
        public async Task<IActionResult> Applications(
            [FromQuery] string? scope = "all", [FromQuery] string? status = null, [FromQuery] Guid? reviewerId = null,
            [FromQuery] bool? assigned = null, [FromQuery] string? search = null,
            [FromQuery] DateTime? createdFrom = null, [FromQuery] DateTime? createdTo = null,
            [FromQuery] string? sort = "newest", [FromQuery] int page = 1, [FromQuery] int pageSize = 50)
        {
            if (page < 1) page = 1;
            if (pageSize < 1) pageSize = 50;
            if (pageSize > 200) pageSize = 200;

            var q = _context.Applications.AsNoTracking().Where(a => a.IsHni == true && !a.IsArchived);
            if (string.Equals(scope, "open", StringComparison.OrdinalIgnoreCase)) q = q.Where(a => Active.Contains(a.Status.ToLower()));
            if (!string.IsNullOrWhiteSpace(status)) { var st = status.Trim().ToLowerInvariant(); q = q.Where(a => a.Status.ToLower() == st); }
            if (reviewerId.HasValue) { var rid = reviewerId.Value; q = q.Where(a => _context.ApplicationAssignments.Any(s => s.ApplicationId == a.Id && s.IsActive && s.ReviewerId == rid)); }
            if (assigned == true) q = q.Where(a => _context.ApplicationAssignments.Any(s => s.ApplicationId == a.Id && s.IsActive));
            if (assigned == false) q = q.Where(a => !_context.ApplicationAssignments.Any(s => s.ApplicationId == a.Id && s.IsActive));
            if (createdFrom.HasValue) { var f = createdFrom.Value.Date; q = q.Where(a => a.CreatedAt >= f); }
            if (createdTo.HasValue) { var t = createdTo.Value.Date.AddDays(1); q = q.Where(a => a.CreatedAt < t); }
            if (!string.IsNullOrWhiteSpace(search))
            {
                var s = search.Trim().ToLowerInvariant();
                q = q.Where(a => a.ApplicationNumber.ToLower().Contains(s) || a.RsaPin.ToLower().Contains(s)
                              || (a.FirstName + " " + a.LastName).ToLower().Contains(s));
            }

            var total = await q.CountAsync();
            IOrderedQueryable<MMGT.API.Models.Application> ordered = (sort ?? "newest").Trim().ToLowerInvariant() switch
            {
                "oldest" => q.OrderBy(a => a.CreatedAt),
                "amount" => q.OrderByDescending(a => a.LoanAmount),
                _ => q.OrderByDescending(a => a.CreatedAt)
            };
            var rows = await ordered.ThenBy(a => a.Id).Skip((page - 1) * pageSize).Take(pageSize)
                .Select(a => new
                {
                    ApplicationId = a.Id, a.ApplicationNumber, a.Status, a.Priority,
                    First = a.FirstName, Middle = a.MiddleName, Last = a.LastName, a.RsaPin,
                    PmiName = a.PmiCompany.PmiName, a.LoanAmount, a.CreatedAt, a.SubmittedAt, a.ResubmittedAt,
                    a.ResubmissionCount, a.UpdatedAt, a.CompletedAt, a.HniCheckedAt
                }).ToListAsync();

            var ids = rows.Select(r => r.ApplicationId).ToList();
            var assignments = await _context.ApplicationAssignments.AsNoTracking()
                .Where(s => ids.Contains(s.ApplicationId) && s.IsActive)
                .Select(s => new { s.ApplicationId, s.ReviewerId, s.AssignedAt }).ToListAsync();
            var reviewerIds = assignments.Select(s => s.ReviewerId).Distinct().ToList();
            var users = await _context.Users.AsNoTracking().Where(u => reviewerIds.Contains(u.Id))
                .Select(u => new { u.Id, u.FirstName, u.LastName }).ToListAsync();

            var now = DateTime.UtcNow;
            var items = rows.Select(r =>
            {
                var asg = assignments.Where(s => s.ApplicationId == r.ApplicationId).OrderByDescending(s => s.AssignedAt).FirstOrDefault();
                var u = asg == null ? null : users.FirstOrDefault(x => x.Id == asg.ReviewerId);
                return new
                {
                    r.ApplicationId, r.ApplicationNumber, r.Status, r.Priority,
                    BorrowerName = string.Join(" ", new[] { r.First, r.Middle, r.Last }.Where(p => !string.IsNullOrWhiteSpace(p))),
                    r.RsaPin, r.PmiName, r.LoanAmount, r.CreatedAt, r.SubmittedAt, r.ResubmittedAt, r.ResubmissionCount, r.UpdatedAt, r.CompletedAt,
                    AgeDays = (int)Math.Floor((now - r.CreatedAt).TotalDays),
                    AssignedReviewerId = asg?.ReviewerId,
                    AssignedReviewerName = u == null ? null : (u.FirstName + " " + u.LastName).Trim(),
                    AssignedAt = asg?.AssignedAt,
                    r.HniCheckedAt
                };
            }).ToList();

            return Ok(new { items, total, page, pageSize, totalPages = (int)Math.Ceiling(total / (double)pageSize) });
        }

        [HttpGet("summary")]
        public async Task<IActionResult> Summary()
        {
            var hniApps = _context.Applications.AsNoTracking().Where(a => a.IsHni == true && !a.IsArchived);
            var byStatus = await hniApps.GroupBy(a => a.Status.ToLower()).Select(g => new { Status = g.Key, Count = g.Count() }).ToListAsync();
            var open = hniApps.Where(a => Active.Contains(a.Status.ToLower()));
            var unassignedOpen = await open.CountAsync(a => !_context.ApplicationAssignments.Any(s => s.ApplicationId == a.Id && s.IsActive));
            var activeApps = _context.Applications.AsNoTracking().Where(a => !a.IsArchived && Active.Contains(a.Status.ToLower()));
            var notChecked = await activeApps.CountAsync(a => a.IsHni == null);
            var activeTotal = await activeApps.CountAsync();
            var lastChecked = await _context.Applications.AsNoTracking().MaxAsync(a => (DateTime?)a.HniCheckedAt);
            return Ok(new
            {
                total = byStatus.Sum(x => x.Count),
                open = byStatus.Where(x => Active.Contains(x.Status)).Sum(x => x.Count),
                unassignedOpen,
                byStatus,
                activeApplications = activeTotal,
                activeNotYetChecked = notChecked,
                lastCheckedAt = lastChecked
            });
        }

        /// <summary>Check one application now (for example after a PIN correction). Returns yes/no only.</summary>
        [HttpPost("refresh/{applicationId:guid}")]
        public async Task<IActionResult> Refresh(Guid applicationId)
        {
            try
            {
                var r = await _hni.RefreshApplicationAsync(applicationId, "Manual", CurrentUserId());
                return Ok(new { isHni = r.IsHni, outcome = r.Outcome.ToString(), checkedAt = r.CheckedAt });
            }
            catch (InvalidOperationException ex) { return BadRequest(new { message = ex.Message }); }
        }
    }
}