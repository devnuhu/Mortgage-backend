using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MMGT.API.Data;

namespace MMGT.API.Services
{
    /// <summary>
    /// Who may see an application and its documents.
    /// The owner, Service Manager, Lead Reviewer and Super Admin: always.
    /// Admin / Reviewer: only while they hold an active assignment on it.
    /// Everyone else: no.
    /// </summary>
    public static class ApplicationAccessGuard
    {
        private static Guid? UserIdOf(ClaimsPrincipal user)
        {
            var c = user.FindFirst("UserId");
            return c != null && Guid.TryParse(c.Value, out var id) ? id : (Guid?)null;
        }

        public static bool HasRole(ClaimsPrincipal user, string role) =>
            user.IsInRole(role) ||
            user.Claims.Any(c => c.Type.EndsWith("role", StringComparison.OrdinalIgnoreCase) && string.Equals(c.Value, role, StringComparison.Ordinal));

        public static bool IsStaff(HttpContext http) =>
            new[] { "SUPER_ADMIN", "ADMIN", "REVIEWER", "LEAD_REVIEWER", "SERVICE_MANAGER" }.Any(r => HasRole(http.User, r));

        /// <summary>True when the signed-in user created the application. An unknown application returns true so the caller can answer 'not found'.</summary>
        public static async Task<bool> IsOwnerAsync(HttpContext http, Guid applicationId)
        {
            var me = UserIdOf(http.User);
            if (me == null) return false;
            var ctx = http.RequestServices.GetRequiredService<ApplicationDbContext>();
            var ownerId = await ctx.Applications.AsNoTracking().Where(a => a.Id == applicationId).Select(a => (Guid?)a.UserId).FirstOrDefaultAsync();
            return ownerId == null || ownerId == me;
        }

        /// <summary>True when the user's only staff role is SERVICE_MANAGER.</summary>
        public static bool IsServiceManagerOnly(HttpContext http) =>
            HasRole(http.User, "SERVICE_MANAGER") &&
            !HasRole(http.User, "LEAD_REVIEWER") && !HasRole(http.User, "SUPER_ADMIN") &&
            !HasRole(http.User, "ADMIN") && !HasRole(http.User, "REVIEWER");

        /// <summary>
        /// Who may open the FILES of an application. Same as CanViewAsync, except a Service Manager only for applications they created.
        /// (A Service Manager can still see the case, its status, review history and rejection reasons through CanViewAsync.)
        /// </summary>
        public static async Task<bool> CanViewDocumentsAsync(HttpContext http, Guid applicationId)
        {
            if (IsServiceManagerOnly(http)) return await IsOwnerAsync(http, applicationId);
            return await CanViewAsync(http, applicationId);
        }
        public static async Task<bool> CanViewAsync(HttpContext http, Guid applicationId)
        {
            var me = UserIdOf(http.User);
            if (me == null) return false;
            var ctx = http.RequestServices.GetRequiredService<ApplicationDbContext>();
            var ownerId = await ctx.Applications.AsNoTracking().Where(a => a.Id == applicationId).Select(a => (Guid?)a.UserId).FirstOrDefaultAsync();
            if (ownerId == null) return true;          // not found: the caller reports that
            if (ownerId == me) return true;
            if (HasRole(http.User, "SERVICE_MANAGER") || HasRole(http.User, "LEAD_REVIEWER") || HasRole(http.User, "SUPER_ADMIN")) return true;
            // Reviewers may VIEW any application (Submissions list and 360 view). This guard only gates reads;
            // deciding and other actions still require the reviewer's own active assignment.
            if (HasRole(http.User, "ADMIN") || HasRole(http.User, "REVIEWER")) return true;
            return false;
        }
    }
}
