using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MMGT.API.Data;

namespace MMGT.API.Controllers
{
    [ApiController]
    [Route("api/notifications")]
    [Authorize]
    public class NotificationsController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        public NotificationsController(ApplicationDbContext context)
        {
            _context = context;
        }

        private Guid? Me()
        {
            var c = User.FindFirst("UserId");
            return c != null && Guid.TryParse(c.Value, out var id) ? id : null;
        }

        /// <summary>My latest messages and the unread count.</summary>
        [HttpGet]
        public async Task<IActionResult> GetMine([FromQuery] int take = 30)
        {
            var me = Me();
            if (me == null) return Unauthorized();
            if (take < 1 || take > 100) take = 30;
            var items = await _context.Notifications.AsNoTracking()
                .Where(n => n.UserId == me.Value)
                .OrderByDescending(n => n.CreatedAt)
                .Take(take)
                .Select(n => new { n.Id, n.Type, n.Title, n.Message, n.ApplicationId, n.LinkPath, n.IsRead, n.CreatedAt })
                .ToListAsync();
            var unread = await _context.Notifications.CountAsync(n => n.UserId == me.Value && !n.IsRead);
            return Ok(new { unreadCount = unread, items });
        }

        [HttpPost("{id:guid}/read")]
        public async Task<IActionResult> MarkRead(Guid id)
        {
            var me = Me();
            if (me == null) return Unauthorized();
            var now = DateTime.UtcNow;
            await _context.Notifications.Where(n => n.Id == id && n.UserId == me.Value && !n.IsRead)
                .ExecuteUpdateAsync(s => s.SetProperty(n => n.IsRead, true).SetProperty(n => n.ReadAt, (DateTime?)now));
            return NoContent();
        }

        [HttpPost("read-all")]
        public async Task<IActionResult> MarkAllRead()
        {
            var me = Me();
            if (me == null) return Unauthorized();
            var now = DateTime.UtcNow;
            await _context.Notifications.Where(n => n.UserId == me.Value && !n.IsRead)
                .ExecuteUpdateAsync(s => s.SetProperty(n => n.IsRead, true).SetProperty(n => n.ReadAt, (DateTime?)now));
            return NoContent();
        }
    }
}