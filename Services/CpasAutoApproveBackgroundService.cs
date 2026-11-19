using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MMGT.API.Data;
using MMGT.API.Models;

namespace MMGT.API.Services
{
    /// <summary>
    /// Approves applications that CPAS says have been paid.
    /// Looks at applications that are Under Review (not archived, not locked). When the member's CPAS currentStatus equals
    /// CpasAutoApprove:MatchStatus (default "Payment Made on CPAS") the application is set to approved, CompletedAt is set (as a manual approval does)
    /// and one review-history row is written by the system user. No email or SMS is sent and documents are not changed.
    ///
    /// Document rule: applications submitted before CpasAutoApprove:DocumentsRequiredFrom (default 2026-08-01, the old one-document process)
    /// are approved on CPAS's word alone. Applications submitted on or after that date also need MandatoryDocumentsComplete.
    /// Use "none" to switch the document rule off.
    ///
    /// Pointer: every run continues after the last application of the previous run (kept in memory), so the whole list is covered
    /// over time. At the end of the list it starts again from the oldest. A restart starts from the oldest.
    ///
    /// Settings: CpasAutoApprove:Enabled (default false), :DryRun (default true, only logs), :IntervalMinutes (30), :MaxPerRun (500),
    /// :MatchStatus, :DocumentsRequiredFrom, :ActorEmail (default cpas.system@accesspensions.ng).
    /// </summary>
    public sealed class CpasAutoApproveBackgroundService : BackgroundService
    {
        private readonly IServiceProvider _sp;
        private readonly IConfiguration _config;
        private readonly ILogger<CpasAutoApproveBackgroundService> _logger;
        private DateTime? _cursor;   // submitted (or created) time of the last application examined in this pass

        public CpasAutoApproveBackgroundService(IServiceProvider sp, IConfiguration config, ILogger<CpasAutoApproveBackgroundService> logger)
        {
            _sp = sp; _config = config; _logger = logger;
        }

        private int Int(string key, int fallback, int min, int max) =>
            int.TryParse(_config[key], out var v) && v >= min && v <= max ? v : fallback;

        private bool Flag(string key, bool fallback) =>
            bool.TryParse(_config[key], out var v) ? v : fallback;

        /// <summary>The date from which documents must also be complete. Null = no document rule.</summary>
        private DateTime? DocumentsRequiredFrom()
        {
            var raw = (_config["CpasAutoApprove:DocumentsRequiredFrom"] ?? string.Empty).Trim();
            if (raw.Length == 0) raw = "2026-08-01";
            if (string.Equals(raw, "none", StringComparison.OrdinalIgnoreCase)) return null;
            if (DateTime.TryParseExact(raw, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var d)) return d;
            _logger.LogWarning("[CPAS-APPROVE] CpasAutoApprove:DocumentsRequiredFrom '{Value}' is not a date (yyyy-MM-dd) or 'none'. Using 2026-08-01.", raw);
            return new DateTime(2026, 8, 1);
        }

        protected override async Task ExecuteAsync(CancellationToken stop)
        {
            if (!Flag("CpasAutoApprove:Enabled", false))
            {
                _logger.LogInformation("[CPAS-APPROVE] Switched off (CpasAutoApprove:Enabled is not true)");
                return;
            }
            var from = DocumentsRequiredFrom();
            _logger.LogInformation("[CPAS-APPROVE] Started: dry run {Dry}, every {Minutes} min, up to {Max} per run, documents required from {From}",
                Flag("CpasAutoApprove:DryRun", true), Int("CpasAutoApprove:IntervalMinutes", 30, 1, 1440), Int("CpasAutoApprove:MaxPerRun", 500, 1, 5000),
                from.HasValue ? from.Value.ToString("yyyy-MM-dd") : "none");
            try { await Task.Delay(TimeSpan.FromSeconds(90), stop); } catch (OperationCanceledException) { return; }

            while (!stop.IsCancellationRequested)
            {
                try { await RunOnceAsync(stop); }
                catch (OperationCanceledException) when (stop.IsCancellationRequested) { break; }
                catch (Exception ex) { _logger.LogError(ex, "[CPAS-APPROVE] Run failed"); }

                try { await Task.Delay(TimeSpan.FromMinutes(Int("CpasAutoApprove:IntervalMinutes", 30, 1, 1440)), stop); }
                catch (OperationCanceledException) { break; }
            }
        }

        private async Task RunOnceAsync(CancellationToken stop)
        {
            var wanted = (_config["CpasAutoApprove:MatchStatus"] ?? "Payment Made on CPAS").Trim();
            if (wanted.Length == 0) return;
            bool dry = Flag("CpasAutoApprove:DryRun", true);
            int max = Int("CpasAutoApprove:MaxPerRun", 500, 1, 5000);
            var from = DocumentsRequiredFrom();

            using var scope = _sp.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var cpas = scope.ServiceProvider.GetRequiredService<ICpasCaseStatusService>();

            var open = db.Applications.AsNoTracking()
                .Where(a => a.Status.ToLower() == "under_review" && !a.IsArchived && !a.IsLocked);

            int heldBack = 0;
            if (from.HasValue)
            {
                var cutoff = from.Value;
                heldBack = await open.CountAsync(a => (a.SubmittedAt ?? a.CreatedAt) >= cutoff && !a.MandatoryDocumentsComplete, stop);
                open = open.Where(a => (a.SubmittedAt ?? a.CreatedAt) < cutoff || a.MandatoryDocumentsComplete);
            }

            var query = open;
            if (_cursor.HasValue)
            {
                var after = _cursor.Value;
                query = query.Where(a => (a.SubmittedAt ?? a.CreatedAt) > after);
            }

            var candidates = await query
                .OrderBy(a => a.SubmittedAt ?? a.CreatedAt)
                .Select(a => new { a.Id, a.ApplicationNumber, a.RsaPin, Key = (a.SubmittedAt ?? a.CreatedAt) })
                .Take(max)
                .ToListAsync(stop);

            if (candidates.Count == 0)
            {
                if (_cursor.HasValue)
                {
                    _cursor = null;
                    _logger.LogInformation("[CPAS-APPROVE] Reached the end of the list. The next run starts again from the oldest.");
                }
                return;
            }

            Guid? actorId = null;
            int checkedCount = 0, matched = 0, approved = 0, failures = 0;
            bool stoppedEarly = false;
            DateTime? lastDone = null;

            foreach (var c in candidates)
            {
                stop.ThrowIfCancellationRequested();
                var previousDone = lastDone;
                lastDone = c.Key;

                string? status;
                try
                {
                    status = await cpas.GetCurrentStatusAsync(c.RsaPin, stop);
                    failures = 0;
                    checkedCount++;
                }
                catch (HttpRequestException)
                {
                    if (++failures >= 5) { _logger.LogWarning("[CPAS-APPROVE] CPAS looks unavailable: run stopped early after {Checked} checks", checkedCount); lastDone = previousDone; stoppedEarly = true; break; }
                    continue;
                }
                catch (TaskCanceledException) when (!stop.IsCancellationRequested)
                {
                    if (++failures >= 5) { _logger.LogWarning("[CPAS-APPROVE] CPAS is too slow: run stopped early after {Checked} checks", checkedCount); lastDone = previousDone; stoppedEarly = true; break; }
                    continue;
                }

                await Task.Delay(150, stop);

                if (string.IsNullOrWhiteSpace(status) || !string.Equals(status.Trim(), wanted, StringComparison.OrdinalIgnoreCase)) continue;
                matched++;

                if (dry)
                {
                    _logger.LogWarning("[CPAS-APPROVE][DRYRUN] would approve {No} (CPAS: {Status})", c.ApplicationNumber, status);
                    continue;
                }

                actorId ??= await EnsureActorAsync(db, stop);
                if (actorId == null) { _logger.LogError("[CPAS-APPROVE] The system user could not be found or created. Run stopped."); lastDone = previousDone; stoppedEarly = true; break; }

                try
                {
                    var app = await db.Applications.FirstOrDefaultAsync(a => a.Id == c.Id, stop);
                    if (app == null || (app.Status ?? string.Empty).Trim().ToLower() != "under_review" || app.IsLocked || app.IsArchived)
                    {
                        db.ChangeTracker.Clear();
                        continue; // someone decided it, locked it or archived it while we were checking
                    }
                    var now = DateTime.UtcNow;
                    app.Status = "approved";
                    app.UpdatedAt = now;
                    app.CompletedAt = now;
                    db.ApplicationReviewHistory.Add(new ApplicationReviewHistory
                    {
                        Id = Guid.NewGuid(),
                        ApplicationId = app.Id,
                        ReviewedById = actorId.Value,
                        Status = "approved",
                        Comments = "Approved automatically: CPAS reports the case as '" + status.Trim() + "'.",
                        CreatedAt = now
                    });
                    await db.SaveChangesAsync(stop);
                    approved++;
                    _logger.LogInformation("[CPAS-APPROVE] Approved {No} (CPAS: {Status})", c.ApplicationNumber, status);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    _logger.LogError(ex, "[CPAS-APPROVE] Could not approve {No}", c.ApplicationNumber);
                }
                finally { db.ChangeTracker.Clear(); }
            }

            // Move the pointer: on to the next group, or back to the start when the end of the list was reached.
            if (!stoppedEarly && candidates.Count < max)
            {
                _cursor = null;
                _logger.LogInformation("[CPAS-APPROVE] Reached the end of the list. The next run starts again from the oldest.");
            }
            else if (lastDone.HasValue)
            {
                _cursor = lastDone;
            }

            _logger.LogInformation("[CPAS-APPROVE] Run finished{Dry}: {Checked} checked, {Matched} matched, {Approved} approved, {Held} held back (new process, documents incomplete)",
                dry ? " (dry run)" : "", checkedCount, matched, approved, heldBack);
        }

        /// <summary>The review history needs a user. This is an inactive account that cannot sign in; it is created once.</summary>
        private async Task<Guid?> EnsureActorAsync(ApplicationDbContext db, CancellationToken ct)
        {
            var email = (_config["CpasAutoApprove:ActorEmail"] ?? "cpas.system@accesspensions.ng").Trim().ToLowerInvariant();
            var existing = await db.Users.AsNoTracking().Where(u => u.Email.ToLower() == email).Select(u => (Guid?)u.Id).FirstOrDefaultAsync(ct);
            if (existing.HasValue) return existing;
            try
            {
                var now = DateTime.UtcNow;
                var user = new User
                {
                    Id = Guid.NewGuid(),
                    Email = email,
                    FirstName = "CPAS",
                    LastName = "Auto-approval",
                    Role = "SYSTEM",
                    PasswordHash = Guid.NewGuid().ToString("N") + Guid.NewGuid().ToString("N"),
                    IsFirstLogin = false,
                    IsActive = false,
                    CreatedAt = now,
                    UpdatedAt = now
                };
                db.Users.Add(user);
                await db.SaveChangesAsync(ct);
                db.ChangeTracker.Clear();
                _logger.LogInformation("[CPAS-APPROVE] Created the system user {Email}", email);
                return user.Id;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                db.ChangeTracker.Clear();
                _logger.LogError(ex, "[CPAS-APPROVE] Could not create the system user {Email}", email);
                return null;
            }
        }
    }
}
