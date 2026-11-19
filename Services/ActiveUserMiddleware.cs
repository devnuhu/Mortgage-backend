using Microsoft.EntityFrameworkCore;
using MMGT.API.Data;

namespace MMGT.API.Services
{
    /// <summary>
    /// Refuses any signed-in request from a user who has been deactivated, even if their login token has not expired yet.
    /// Runs after authentication. Anonymous requests (login, forgot password) pass straight through.
    /// </summary>
    public class ActiveUserMiddleware
    {
        private readonly RequestDelegate _next;

        public ActiveUserMiddleware(RequestDelegate next)
        {
            _next = next;
        }

        public async Task InvokeAsync(HttpContext context, ApplicationDbContext db)
        {
            if (context.User?.Identity?.IsAuthenticated == true)
            {
                var claim = context.User.FindFirst("UserId")?.Value;
                if (Guid.TryParse(claim, out var userId))
                {
                    var active = await db.Users.AsNoTracking().AnyAsync(u => u.Id == userId && u.IsActive);
                    if (!active)
                    {
                        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                        context.Response.ContentType = "application/json";
                        await context.Response.WriteAsync("{\"message\":\"This account has been deactivated. Please contact your administrator.\"}");
                        return;
                    }
                }
            }

            await _next(context);
        }
    }
}