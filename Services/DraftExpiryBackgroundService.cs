using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MMGT.API.Models;
using MMGT.API.Data;

namespace MMGT.API.Services
{
    /// <summary>
    /// Expires drafts that were never submitted and have had no activity for DraftRetention:Days.
    /// Removes the uploaded document content, keeps the application row (status "expired") and one history entry for audit.
    /// DraftRetention:DryRun (default true) only logs what would happen.
    /// </summary>
    public sealed class DraftExpiryBackgroundService : BackgroundService
    {
        private readonly IServiceScopeFactory _scopes;
        private readonly IConfiguration _config;
        private readonly IWebHostEnvironment _env;
        private readonly ILogger<DraftExpiryBackgroundService> _logger;

        public DraftExpiryBackgroundService(IServiceScopeFactory scopes, IConfiguration config, IWebHostEnvironment env, ILogger<DraftExpiryBackgroundService> logger)
        {
            _scopes = scopes;
            _config = config;
            _env = env;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken ct)
        {
            try { await Task.Delay(TimeSpan.FromMinutes(2), ct); } catch (OperationCanceledException) { return; }
            while (!ct.IsCancellationRequested)
            {
                try { await RunOnceAsync(ct); }
                catch (OperationCanceledException) { break; }
                catch (Exception ex) { _logger.LogError(ex, "[DRAFT-EXPIRY] run failed"); }
                try { await Task.Delay(NextRunDelay(), ct); } catch (OperationCanceledException) { break; }
            }
        }

        private TimeSpan NextRunDelay()
        {
            int hour = _config.GetValue<int>("DraftRetention:RunHourUtc", 1);
            if (hour < 0 || hour > 23) hour = 1;
            var now = DateTime.UtcNow;
            var next = now.Date.AddHours(hour);
            if (next <= now.AddMinutes(30)) next = next.AddDays(1);
            return next - now;
        }
        private async Task RunOnceAsync(CancellationToken ct)
        {
            if (!_config.GetValue<bool>("DraftRetention:Enabled", true)) return;
            int days = _config.GetValue<int>("DraftRetention:Days", 14);
            if (days < 7) days = 7;
            bool dry = _config.GetValue<bool>("DraftRetention:DryRun", true);

            using var scope = _scopes.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var now = DateTime.UtcNow;
            var cutoff = now.AddDays(-days);

            var candidates = await db.Applications.AsNoTracking()
                .Where(a => a.Status.ToLower() == "draft" && a.SubmittedAt == null && !a.IsArchived && !a.IsLocked && a.UpdatedAt < cutoff)
                .OrderBy(a => a.UpdatedAt)
                .Select(a => new { a.Id, a.ApplicationNumber, a.UpdatedAt })
                .Take(500)
                .ToListAsync(ct);

            int expired = 0, filesRemoved = 0, withFiles = 0, totalDocs = 0, maxIdle = 0, minIdle = int.MaxValue;
            foreach (var c in candidates)
            {
                ct.ThrowIfCancellationRequested();
                try
                {
                    var lastDoc = await db.ApplicationDocuments.AsNoTracking()
                        .Where(d => d.ApplicationId == c.Id)
                        .MaxAsync(d => (DateTime?)d.CreatedAt, ct);
                    var last = (lastDoc.HasValue && lastDoc.Value > c.UpdatedAt) ? lastDoc.Value : c.UpdatedAt;
                    if (last >= cutoff) continue;

                    var docs = await db.ApplicationDocuments.AsNoTracking()
                        .Where(d => d.ApplicationId == c.Id)
                        .Select(d => new { d.Id, d.FilePath })
                        .ToListAsync(ct);
                    int idle = (int)(now - last).TotalDays;
                    if (docs.Count > 0) withFiles++;
                    totalDocs += docs.Count;
                    if (idle > maxIdle) maxIdle = idle;
                    if (idle < minIdle) minIdle = idle;

                    if (dry)
                    {
                        _logger.LogWarning("[DRAFT-EXPIRY][DRYRUN] would expire {No} (idle {Idle} days, {Docs} documents)", c.ApplicationNumber, idle, docs.Count);
                        expired++;
                        continue;
                    }

                    // 1) remove stored content (rows stay for audit: name, type, size). Safe to repeat if a later step fails.
                    await db.ApplicationDocuments.Where(d => d.ApplicationId == c.Id)
                        .ExecuteUpdateAsync(s => s.SetProperty(d => d.FileContent, (byte[]?)null).SetProperty(d => d.FilePath, (string?)null), ct);

                    // 2) mark the application expired and add the audit entry
                    var app = await db.Applications.FirstAsync(a => a.Id == c.Id, ct);
                    if (!string.Equals(app.Status, "draft", StringComparison.OrdinalIgnoreCase) || app.SubmittedAt != null) { db.ChangeTracker.Clear(); continue; }
                    app.Status = "expired";
                    app.UpdatedAt = now;
                    db.Set<ApplicationReviewHistory>().Add(new ApplicationReviewHistory
                    {
                        Id = Guid.NewGuid(),
                        ApplicationId = app.Id,
                        Status = "expired",
                        Comments = $"Draft expired after {idle} days without activity. {docs.Count} uploaded document(s) were removed. The RSA PIN was released.",
                        CreatedAt = now,
                        ReviewedById = app.UserId
                    });
                    await db.SaveChangesAsync(ct);
                    db.ChangeTracker.Clear();

                    // 3) remove legacy files from disk (best effort, only inside wwwroot)
                    foreach (var d in docs)
                    {
                        if (!string.IsNullOrWhiteSpace(d.FilePath) && TryDeleteLegacyFile(d.FilePath!)) filesRemoved++;
                    }
                    expired++;
                    _logger.LogWarning("[DRAFT-EXPIRY] expired {No} (idle {Idle} days, {Docs} documents removed)", c.ApplicationNumber, idle, docs.Count);
                }
                catch (OperationCanceledException) { throw; }
                catch (Exception ex)
                {
                    db.ChangeTracker.Clear();
                    _logger.LogError(ex, "[DRAFT-EXPIRY] failed for {No}", c.ApplicationNumber);
                }
            }
            _logger.LogInformation("[DRAFT-EXPIRY] run finished. mode={Mode}, days={Days}, drafts={Count}, withFiles={WithFiles}, documents={Docs}, idleDays={Min}-{Max}, legacyFilesDeleted={Files}", dry ? "DRYRUN" : "LIVE", days, expired, withFiles, totalDocs, minIdle == int.MaxValue ? 0 : minIdle, maxIdle, filesRemoved);
        }

        private bool TryDeleteLegacyFile(string stored)
        {
            try
            {
                var webRoot = _env.WebRootPath ?? Path.Combine(_env.ContentRootPath, "wwwroot");
                var sep = Path.DirectorySeparatorChar;
                var root = Path.GetFullPath(webRoot).TrimEnd(sep) + sep;
                var norm = stored.Replace('/', sep).Replace('\\', sep);
                var idx = norm.IndexOf("wwwroot", StringComparison.OrdinalIgnoreCase);
                var rel = (idx >= 0 ? norm.Substring(idx + "wwwroot".Length) : norm).TrimStart(sep);
                var full = Path.GetFullPath(Path.Combine(root, rel));
                if (!full.StartsWith(root, StringComparison.OrdinalIgnoreCase)) return false;
                if (!File.Exists(full)) return false;
                File.Delete(full);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[DRAFT-EXPIRY] could not delete a legacy file");
                return false;
            }
        }
    }
}