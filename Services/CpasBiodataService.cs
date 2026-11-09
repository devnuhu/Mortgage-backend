using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using MMGT.API.Exceptions;
using MMGT.API.Services.Interfaces;

namespace MMGT.API.Services
{
    /// <summary>
    /// Reads a member's name and phone number from CPAS: GET {CpasApi:BaseUrl}/Portal/GetBiodata/{RsaPin}
    /// (the same call the web app makes). No credentials are needed.
    /// Fields read: fname, mname, lname, phonenumber. Nothing personal is written to the logs.
    /// </summary>
    public sealed class CpasBiodataService : ICpasBiodataService
    {
        private const string DefaultBaseUrl = "https://cpasapi.access-armpensions.com/api";
        private const string Unavailable = "We could not verify your RSA details right now. Please try again in a few minutes.";
        private const string BadPhone = "The phone number we hold for you is missing or not valid. Please visit our nearest branch to update your details, then try again.";

        private readonly HttpClient _http;
        private readonly ILogger<CpasBiodataService> _logger;

        public CpasBiodataService(HttpClient http, IConfiguration configuration, ILogger<CpasBiodataService> logger)
        {
            _http = http;
            _logger = logger;

            var baseUrl = configuration["CpasApi:BaseUrl"];
            if (string.IsNullOrWhiteSpace(baseUrl)) baseUrl = DefaultBaseUrl;
            _http.BaseAddress = new Uri(baseUrl.TrimEnd('/') + "/");
            _http.Timeout = TimeSpan.FromSeconds(10);
        }

        public async Task<CpasBiodata> GetBiodataAsync(string rsaPin, CancellationToken cancellationToken = default)
        {
            var pin = (rsaPin ?? string.Empty).Trim().ToUpperInvariant();
            if (pin.Length == 0)
                throw new BusinessRuleException("RSA PIN is required.");
            if (!pin.StartsWith("PEN")) pin = "PEN" + pin;

            HttpResponseMessage response;
            string body;
            try
            {
                response = await _http.GetAsync("Portal/GetBiodata/" + Uri.EscapeDataString(pin), cancellationToken);
                body = await response.Content.ReadAsStringAsync(cancellationToken);
            }
            catch (Exception ex) when (ex is HttpRequestException || ex is OperationCanceledException)
            {
                _logger.LogError(ex, "[CPAS] Could not reach CPAS to look up RSA details");
                throw new BusinessRuleException(Unavailable);
            }

            if (response.StatusCode == HttpStatusCode.NotFound)
                throw new BusinessRuleException("No active RSA account was found for this RSA PIN.");

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("[CPAS] RSA lookup answered with status {Status}", (int)response.StatusCode);
                throw new BusinessRuleException(Unavailable);
            }

            JsonDocument doc;
            try
            {
                doc = JsonDocument.Parse(body);
            }
            catch (JsonException ex)
            {
                _logger.LogError(ex, "[CPAS] RSA lookup reply could not be read");
                throw new BusinessRuleException(Unavailable);
            }

            using (doc)
            {
                var root = doc.RootElement;
                if (root.ValueKind != JsonValueKind.Object)
                {
                    _logger.LogError("[CPAS] RSA lookup reply was not the expected shape");
                    throw new BusinessRuleException(Unavailable);
                }

                var first = Read(root, "fname");
                var last = Read(root, "lname");
                if (first == null || last == null)
                    throw new BusinessRuleException("The RSA record for this PIN has no name on file. Please contact support.");

                var middle = Read(root, "mname");

                // Text messages go to this number, so it must be a usable Nigerian mobile number.
                // Missing or not valid on CPAS -> the member is asked to update their details at a branch.
                var rawPhone = Read(root, "phonenumber", "phone", "msisdn", "mobile");
                var phone = rawPhone == null ? string.Empty : PhoneNumberNormalizer.Normalize(rawPhone);
                if (!Regex.IsMatch(phone, @"^234[789]\d{9}$"))
                    throw new BusinessRuleException(BadPhone);

                return new CpasBiodata(Cut(first, 100)!, Cut(middle, 100), Cut(last, 100)!, phone);
            }
        }

        private static string? Cut(string? text, int max)
        {
            if (text == null) return null;
            return text.Length <= max ? text : text.Substring(0, max);
        }

        private static string? Read(JsonElement root, params string[] names)
        {
            foreach (var name in names)
            {
                foreach (var prop in root.EnumerateObject())
                {
                    if (!string.Equals(prop.Name, name, StringComparison.OrdinalIgnoreCase)) continue;

                    string? text = prop.Value.ValueKind switch
                    {
                        JsonValueKind.String => prop.Value.GetString(),
                        JsonValueKind.Number => prop.Value.GetRawText(),
                        _ => null
                    };
                    text = text?.Trim();
                    if (!string.IsNullOrEmpty(text)) return text;
                }
            }
            return null;
        }
    }
}