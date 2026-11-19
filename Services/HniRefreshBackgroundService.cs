using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MMGT.API.Data;

namespace MMGT.API.Services
{
    /// <summary>Fills in and refreshes the HNI flag of active applications in small batches (newest first). Slow and gentle on CPAS by design.</summary>
    public sealed class HniRefreshBackgroundService : BackgroundService
    {
        private static readonly string[] Active = { "submitted", "under_review", "pending", "returned", "resubmitted" };
        private readonly IServiceProvider _sp;
        private readonly IConfiguration _config;
        private readonly ILogger<HniRefreshBackgroundService> _logger;

        public HniRefreshBackgroundService(IServiceProvider sp, IConfiguration config, ILogger<HniRefreshBackgroundService> logger)
        {
            _sp = sp; _config = config; _logger = logger;
        }

        private int Int(string key, int fallback, int min, int max) =>
            int.TryParse(_config[key], out var v) && v >= min && v <= max ? v : fallback;

        protected override async Task ExecuteAsync(CancellationToken stop)
        {
            if (string.Equals(_config["Hni:Enabled"], "false", StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogInformation("[HNI] Background refresh is switched off (Hni:Enabled=false)");
                return;
            }
            try { await Task.Delay(TimeSpan.FromSeconds(60), stop); } catch (OperationCanceledException) { return; }

            while (!stop.IsCancellationRequested)
            {
                try { await RunBatchAsync(stop); }
                catch (OperationCanceledException) when (stop.IsCancellationRequested) { break; }
                catch (Exception ex) { _logger.LogError("[HNI] Background batch failed: {Message}", ex.Message); }

                try { await Task.Delay(TimeSpan.FromMinutes(Int("Hni:IntervalMinutes", 2, 1, 120)), stop); }
                catch (OperationCanceledException) { break; }
            }
        }

        private async Task RunBatchAsync(CancellationToken stop)
        {
            using var scope = _sp.CreateScope();
            var ctx = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var hni = scope.ServiceProvider.GetRequiredService<IHniService>();

            var now = DateTime.UtcNow;
            var staleBefore = now.AddHours(-Int("Hni:RefreshHours", 12, 1, 720));
            var retryBefore = now.AddMinutes(-15);
            var batch = Int("Hni:BatchSize", 100, 1, 500);

            var ids = await ctx.Applications.AsNoTracking()
                .Where(a => !a.IsArchived && Active.Contains(a.Status.ToLower()))
                .Where(a => a.HniCheckedAt == null || a.HniCheckedAt < staleBefore)
                .Where(a => a.HniLastAttemptAt == null || a.HniLastAttemptAt < retryBefore)
                .OrderBy(a => a.HniCheckedAt == null ? 0 : 1)
                .ThenByDescending(a => a.SubmittedAt ?? a.CreatedAt)
                .Select(a => a.Id).Take(batch).ToListAsync(stop);
            if (ids.Count == 0) return;

            int done = 0, failures = 0;
            foreach (var id in ids)
            {
                stop.ThrowIfCancellationRequested();
                var r = await hni.RefreshApplicationAsync(id, "Background", null, stop);
                ctx.ChangeTracker.Clear();
                if (r.Outcome == HniOutcome.CpasUnreachable)
                {
                    if (++failures >= 5) { _logger.LogWarning("[HNI] CPAS looks unavailable: batch stopped early after {Done} checks", done); break; }
                }
                else { failures = 0; done++; }
                await Task.Delay(150, stop);
            }
            _logger.LogInformation("[HNI] Background batch finished: {Done} of {Total} checked", done, ids.Count);
        }
    }
}