using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace MMGT.API.Services
{
    public interface ICpasCaseStatusService
    {
        /// <summary>The member's current case status from CPAS, or null when there is none. Throws HttpRequestException when CPAS cannot be reached.</summary>
        Task<string?> GetCurrentStatusAsync(string rsaPin, CancellationToken ct = default);
    }

    public sealed class CpasCaseStatusService : ICpasCaseStatusService
    {
        private readonly HttpClient _http;
        private readonly IConfiguration _config;
        private readonly ILogger<CpasCaseStatusService> _logger;

        public CpasCaseStatusService(HttpClient http, IConfiguration config, ILogger<CpasCaseStatusService> logger)
        {
            _http = http; _config = config; _logger = logger;
            CpasAuth.Apply(_http, _config);
        }

        public async Task<string?> GetCurrentStatusAsync(string rsaPin, CancellationToken ct = default)
        {
            var baseUrl = (_config["CpasApi:BaseUrl"] ?? string.Empty).Trim().TrimEnd('/');
            if (baseUrl.Length == 0) throw new HttpRequestException("CpasApi:BaseUrl is not configured");
            using var resp = await _http.GetAsync($"{baseUrl}/Website/CaseTracker/{Uri.EscapeDataString(rsaPin.Trim())}", ct);
            if (resp.StatusCode == System.Net.HttpStatusCode.NotFound) return null;
            if (!resp.IsSuccessStatusCode)
            {
                _logger.LogWarning("[CPAS] Case tracker answered with status {Status}", (int)resp.StatusCode);
                throw new HttpRequestException("CPAS case tracker answered with an error");
            }
            var body = await resp.Content.ReadAsStringAsync(ct);
            try
            {
                using var doc = JsonDocument.Parse(body);
                if (doc.RootElement.ValueKind != JsonValueKind.Object) return null;
                foreach (var p in doc.RootElement.EnumerateObject())
                {
                    if (string.Equals(p.Name, "currentStatus", StringComparison.OrdinalIgnoreCase) && p.Value.ValueKind == JsonValueKind.String)
                    {
                        var v = p.Value.GetString();
                        return string.IsNullOrWhiteSpace(v) ? null : v;
                    }
                }
            }
            catch (JsonException) { _logger.LogWarning("[CPAS] Case tracker reply was not valid JSON"); }
            return null;
        }
    }
}