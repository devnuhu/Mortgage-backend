using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MMGT.API.Models;
using MMGT.API.Data;

namespace MMGT.API.Controllers
{
    /// <summary>A processor's own figures. It never returns anyone else's data.</summary>
    [ApiController]
    [Route("api/assignments/mine/insights")]
    [Authorize(Roles = "ADMIN,REVIEWER")]
    public class MyInsightsController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        public MyInsightsController(ApplicationDbContext context)
        {
            _context = context;
        }

        private Guid? Me()
        {
            var c = User.FindFirst("UserId");
            return c != null && Guid.TryParse(c.Value, out var id) ? id : null;
        }

        [HttpGet]
        public async Task<IActionResult> Get()
        {
            var me = Me();
            if (me == null) return Unauthorized();

            // All dates below are Nigerian time (UTC+1).
            var localNow = DateTime.UtcNow.AddHours(1);
            var today = localNow.Date;
            var weekStart = today.AddDays(-(((int)today.DayOfWeek + 6) % 7)); // Monday
            var monthStart = new DateTime(today.Year, today.Month, 1);
            var firstWeek = weekStart.AddDays(-7 * 7);
            var sinceUtc = firstWeek.AddHours(-1);

            var raw = await _context.Set<ApplicationReviewHistory>().AsNoTracking()
                .Where(h => h.ReviewedById == me.Value && h.CreatedAt >= sinceUtc
                    && (h.Status.ToLower() == "approved" || h.Status.ToLower() == "rejected"))
                .Select(h => new { Status = h.Status.ToLower(), h.CreatedAt, SubmittedAt = h.Application.SubmittedAt })
                .ToListAsync();

            var rows = raw.Select(r => new { r.Status, Local = r.CreatedAt.AddHours(1), r.SubmittedAt }).ToList();

            object Count(DateTime from) => new
            {
                approved = rows.Count(r => r.Local >= from && r.Status == "approved"),
                rejected = rows.Count(r => r.Local >= from && r.Status == "rejected"),
                total = rows.Count(r => r.Local >= from)
            };

            var monthRows = rows.Where(r => r.Local >= monthStart && r.SubmittedAt != null).ToList();
            var days = monthRows
                .Select(r => (r.Local - r.SubmittedAt!.Value.AddHours(1)).TotalDays)
                .Where(d => d >= 0)
                .OrderBy(d => d)
                .ToList();
            double? average = days.Count == 0 ? (double?)null : Math.Round(days.Average(), 1);
            double? median = null;
            if (days.Count > 0)
            {
                var mid = days.Count / 2;
                median = Math.Round(days.Count % 2 == 1 ? days[mid] : (days[mid - 1] + days[mid]) / 2.0, 1);
            }

            var weekly = Enumerable.Range(0, 8).Select(i =>
            {
                var start = firstWeek.AddDays(7 * i);
                var end = start.AddDays(7);
                return new
                {
                    weekStart = start.ToString("yyyy-MM-dd"),
                    approved = rows.Count(r => r.Local >= start && r.Local < end && r.Status == "approved"),
                    rejected = rows.Count(r => r.Local >= start && r.Local < end && r.Status == "rejected")
                };
            }).ToList();

            return Ok(new
            {
                asOf = localNow.ToString("yyyy-MM-dd HH:mm"),
                today = Count(today),
                thisWeek = Count(weekStart),
                thisMonth = Count(monthStart),
                averageDaysToDecide = average,
                medianDaysToDecide = median,
                decisionsMeasured = days.Count,
                weekly
            });
        }
    }
}