using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using MMGT.API.Data;
using MMGT.API.Models;

namespace MMGT.API.Services
{
    /// <summary>
    /// Refuses any change to an application that is locked by an open step-down request,
    /// or that has already been stepped down (archived). Reading is never blocked.
    /// The message is returned to the screen as a normal "bad request" message.
    /// </summary>
    public static class ApplicationLockGuard
    {
        public const string LockedMessage =
            "This application is locked because a step-down request is in progress. It cannot be changed until the request is closed.";
        public const string ArchivedMessage =
            "This application has been stepped down and can no longer be changed.";

        // Use when the application row is already loaded.
        // FindAsync does not apply the "hide archived" filter, so the archived flag is checked here too.
        public static void EnsureOpen(Application? application)
        {
            if (application == null) return;
            if (application.IsArchived) throw new InvalidOperationException(ArchivedMessage);
            if (application.IsLocked) throw new InvalidOperationException(LockedMessage);
        }

        // Use when only the application id is known (documents, files).
        // Looks past the archived filter so a stepped-down application is still refused, not "not found".
        public static async Task EnsureOpenAsync(ApplicationDbContext context, Guid applicationId)
        {
            var flags = await context.Applications
                .IgnoreQueryFilters()
                .AsNoTracking()
                .Where(a => a.Id == applicationId)
                .Select(a => new { a.IsLocked, a.IsArchived })
                .FirstOrDefaultAsync();
            if (flags == null) return;
            if (flags.IsArchived) throw new InvalidOperationException(ArchivedMessage);
            if (flags.IsLocked) throw new InvalidOperationException(LockedMessage);
        }
    }
}
