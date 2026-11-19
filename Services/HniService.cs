using System.Globalization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using MMGT.API.Data;
using MMGT.API.Models;

namespace MMGT.API.Services
{
    public enum HniOutcome { Checked, NoBalance, InvalidPin, CpasUnreachable }

    public sealed record HniResult(HniOutcome Outcome, bool? IsHni, decimal Threshold, DateTime CheckedAt);

    public interface IHniService
    {
        decimal Threshold { get; }
        /// <summary>Asks CPAS for the application's RSA balance and stores only the yes/no result. Never returns or stores the balance.</summary>
        Task<HniResult> RefreshApplicationAsync(Guid applicationId, string source, Guid? byUserId, CancellationToken ct = default);
    }

    public sealed class HniService : IHniService
    {
        private static readonly Regex PinShape = new("^PEN[A-Z0-9]{12}$", RegexOptions.Compiled);
        private readonly HttpClient _http;
        private readonly IConfiguration _config;
        private readonly ApplicationDbContext _context;
        private readonly ILogger<HniService> _logger;

        public HniService(HttpClient http, IConfiguration config, ApplicationDbContext context, ILogger<HniService> logger)
        {
            _http = http; _config = config; _context = context; _logger = logger;
            CpasAuth.Apply(_http, _config);
        }

        public decimal Threshold =>
            decimal.TryParse(_config["Hni:ThresholdNaira"], NumberStyles.Number, CultureInfo.InvariantCulture, out var t) && t > 0 ? t : 50_000_000m;

        public async Task<HniResult> RefreshApplicationAsync(Guid applicationId, string source, Guid? byUserId, CancellationToken ct = default)
        {
            var app = await _context.Applications.FirstOrDefaultAsync(a => a.Id == applicationId, ct);
            if (app == null) throw new InvalidOperationException("Application not found");

            var now = DateTime.UtcNow;
            var threshold = Threshold;
            var wasChecked = app.HniCheckedAt.HasValue;
            var previous = app.IsHni;
            app.HniLastAttemptAt = now;

            var pin = (app.RsaPin ?? string.Empty).Trim().ToUpperInvariant();
            HniOutcome outcome;
            bool? flag = null;

            if (!PinShape.IsMatch(pin))
            {
                outcome = HniOutcome.InvalidPin;
                app.IsHni = null;
                app.HniCheckedAt = now;
                app.HniThreshold = threshold;
            }
            else
            {
                var (o, balance) = await FetchBalanceAsync(pin, ct);
                outcome = o;
                if (o == HniOutcome.Checked) flag = balance >= threshold;
                else if (o == HniOutcome.NoBalance) flag = false;

                if (o == HniOutcome.Checked || o == HniOutcome.NoBalance)
                {
                    app.IsHni = flag;
                    app.HniCheckedAt = now;
                    app.HniThreshold = threshold;
                }
                // CpasUnreachable: the old flag and HniCheckedAt are left as they were (unknown stays unknown).
            }

            if (outcome != HniOutcome.CpasUnreachable && (!wasChecked || previous != app.IsHni))
            {
                _context.ApplicationHniChecks.Add(new ApplicationHniCheck
                {
                    ApplicationId = app.Id, IsHni = app.IsHni, Threshold = threshold, Outcome = outcome.ToString(),
                    Source = source, PreviousIsHni = previous, CheckedAt = now, CheckedById = byUserId
                });
            }
            await _context.SaveChangesAsync(ct);
            return new HniResult(outcome, app.IsHni, threshold, now);
        }

        private async Task<(HniOutcome, decimal?)> FetchBalanceAsync(string pin, CancellationToken ct)
        {
            var baseUrl = (_config["CpasApi:BaseUrl"] ?? string.Empty).Trim().TrimEnd('/');
            if (baseUrl.Length == 0)
            {
                _logger.LogError("[HNI] CpasApi:BaseUrl is not configured");
                return (HniOutcome.CpasUnreachable, null);
            }
            try
            {
                using var resp = await _http.GetAsync($"{baseUrl}/Transactions/RsaBalance/{Uri.EscapeDataString(pin)}", ct);
                if (resp.StatusCode == System.Net.HttpStatusCode.NotFound) return (HniOutcome.NoBalance, null);
                if (!resp.IsSuccessStatusCode)
                {
                    _logger.LogWarning("[HNI] CPAS balance lookup answered with status {Status}", (int)resp.StatusCode);
                    return (HniOutcome.CpasUnreachable, null);
                }
                var body = await resp.Content.ReadAsStringAsync(ct);
                var balance = ParseBalance(body);
                return balance.HasValue ? (HniOutcome.Checked, balance) : (HniOutcome.NoBalance, null);
            }
            catch (Exception ex) when (!ct.IsCancellationRequested)
            {
                _logger.LogWarning("[HNI] Could not reach CPAS for a balance lookup: {Message}", ex.Message);
                return (HniOutcome.CpasUnreachable, null);
            }
        }

        // CPAS answers an object with currentBalance (string). Same tolerance as the old frontend code: balance / amount, or a bare value.
        private static decimal? ParseBalance(string body)
        {
            if (string.IsNullOrWhiteSpace(body)) return null;
            string? raw = null;
            try
            {
                using var doc = JsonDocument.Parse(body);
                var root = doc.RootElement;
                if (root.ValueKind == JsonValueKind.Object)
                {
                    foreach (var wanted in new[] { "currentBalance", "balance", "amount" })
                    {
                        foreach (var p in root.EnumerateObject())
                        {
                            if (!string.Equals(p.Name, wanted, StringComparison.OrdinalIgnoreCase)) continue;
                            if (p.Value.ValueKind == JsonValueKind.Null) continue;
                            raw = p.Value.ValueKind == JsonValueKind.String ? p.Value.GetString() : p.Value.GetRawText();
                            break;
                        }
                        if (raw != null) break;
                    }
                }
                else if (root.ValueKind == JsonValueKind.String) raw = root.GetString();
                else if (root.ValueKind == JsonValueKind.Number) raw = root.GetRawText();
            }
            catch (JsonException) { raw = body; }
            if (string.IsNullOrWhiteSpace(raw)) return null;
            var cleaned = Regex.Replace(raw, "[^0-9.-]", string.Empty);
            return decimal.TryParse(cleaned, NumberStyles.Float, CultureInfo.InvariantCulture, out var d) ? d : null;
        }
    }
}