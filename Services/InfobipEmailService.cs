using System.Text;
using System.Text.Json;
using System.Net;
using MMGT.API.Services.Interfaces;
using Microsoft.Extensions.Logging;

namespace MMGT.API.Services
{
    public class InfobipEmailService : IEmailService
    {
        private readonly HttpClient _http;
        private readonly IConfiguration _config;
        private readonly ILogger<InfobipEmailService> _logger;

        public InfobipEmailService(HttpClient http, IConfiguration config, ILogger<InfobipEmailService> logger)
        {
            _http = http;
            _config = config;
            _logger = logger;
        }

        public async Task SendFirstLoginLinkAsync(Guid userId, string toEmail, string token, CancellationToken ct = default)
        {
            var baseUrl = _config["INFOBIP_BASE_URL"]?.TrimEnd('/');
            var apiKey = _config["INFOBIP_API_KEY"];
            var sender = _config["INFOBIP_SENDER"] ?? "no-reply@yourdomain.com";
            var frontend = _config["FRONTEND_URL"] ?? "http://localhost:5173";

            if (string.IsNullOrWhiteSpace(baseUrl) || string.IsNullOrWhiteSpace(apiKey))
            {
                _logger.LogWarning("Infobip not configured (INFOBIP_BASE_URL or INFOBIP_API_KEY missing). Skipping send.");
                return;
            }

            var link = $"{frontend}/first-login?uid={userId}&token={Uri.EscapeDataString(token)}";

            var payload = new
            {
                from = sender,
                to = new[] { new { email = toEmail } },
                subject = "Complete your account setup",
                text = $"Hello,\n\nOpen the link to set your password: {link}\n\nThis link expires in 2 hours.",
                html = $"<p>Hello,</p><p>Click the link below to set your password (expires in 2 hours):</p><p><a href=\"{link}\">{link}</a></p>"
            };

            var req = new HttpRequestMessage(HttpMethod.Post, $"{baseUrl}/email/2/send");
            req.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("App", apiKey);
            req.Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

            var res = await _http.SendAsync(req, ct);
            var body = await res.Content.ReadAsStringAsync(ct);

            if (!res.IsSuccessStatusCode)
            {
                _logger.LogError("Infobip send failed: {Status} {Body}", res.StatusCode, body);
                throw new Exception($"Infobip send failed: {res.StatusCode}");
            }

            _logger.LogInformation("Infobip accepted message for {Email}", toEmail);
        }

        public async Task SendApplicationCreatedNotificationAsync(string toEmail, Guid applicationId, string applicationNumber, string applicantName, CancellationToken ct = default)
        {
            var baseUrl = _config["INFOBIP_BASE_URL"]?.TrimEnd('/');
            var apiKey = _config["INFOBIP_API_KEY"];
            var sender = _config["INFOBIP_SENDER"] ?? "no-reply@yourdomain.com";
            var frontend = _config["FRONTEND_URL"] ?? "http://localhost:5173";

            if (string.IsNullOrWhiteSpace(baseUrl) || string.IsNullOrWhiteSpace(apiKey))
            {
                _logger.LogWarning("Infobip not configured (INFOBIP_BASE_URL or INFOBIP_API_KEY missing). Skipping send.");
                return;
            }

            var appLink = $"{frontend}/applications/{applicationId}";

            var payload = new
            {
                from = sender,
                to = new[] { new { email = toEmail } },
                subject = $"New application received: {applicationNumber}",
                text = $"A new application ({applicationNumber}) was created by {applicantName}. View: {appLink}",
                html = $"<div style=\"font-family: Arial, sans-serif; color: #222;\">" +
                       $"<h2 style=\"color:#1a73e8;\">New Application Received</h2>" +
                       $"<p>Dear Partner,</p>" +
                       $"<p>We have received a new application with number <strong>{WebUtility.HtmlEncode(applicationNumber)}</strong> submitted by <strong>{WebUtility.HtmlEncode(applicantName)}</strong>.</p>" +
                       $"<p>You can review the application by clicking the link below:</p>" +
                       $"<p><a href=\"{appLink}\">View Application {WebUtility.HtmlEncode(applicationNumber)}</a></p>" +
                       $"<p>This is an automated notification from Mortgage Management Portal.</p>" +
                       $"</div>"
            };

            var req = new HttpRequestMessage(HttpMethod.Post, $"{baseUrl}/email/2/send");
            req.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("App", apiKey);
            req.Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

            var res = await _http.SendAsync(req, ct);
            var body = await res.Content.ReadAsStringAsync(ct);

            if (!res.IsSuccessStatusCode)
            {
                _logger.LogError("Infobip send failed: {Status} {Body}", res.StatusCode, body);
                throw new Exception($"Infobip send failed: {res.StatusCode}");
            }

            _logger.LogInformation("Infobip accepted application notification for {Email}", toEmail);
        }

        public async Task SendApplicationStatusUpdatedAsync(string toEmail, Guid applicationId, string applicationNumber, string applicantName, string newStatus, string? comments, CancellationToken ct = default)
        {
            var baseUrl = _config["INFOBIP_BASE_URL"]?.TrimEnd('/');
            var apiKey = _config["INFOBIP_API_KEY"];
            var sender = _config["INFOBIP_SENDER"] ?? "no-reply@yourdomain.com";
            var frontend = _config["FRONTEND_URL"] ?? "http://localhost:5173";

            if (string.IsNullOrWhiteSpace(baseUrl) || string.IsNullOrWhiteSpace(apiKey))
            {
                _logger.LogWarning("Infobip not configured (INFOBIP_BASE_URL or INFOBIP_API_KEY missing). Skipping send.");
                return;
            }

            var appLink = $"{frontend}/applications/{applicationId}";
            var statusCapitalized = string.IsNullOrEmpty(newStatus) ? newStatus : char.ToUpperInvariant(newStatus[0]) + newStatus.Substring(1);
            var commentsHtml = string.IsNullOrWhiteSpace(comments) ? "<p>No additional comments provided.</p>" : $"<p><strong>Comments:</strong> {WebUtility.HtmlEncode(comments)}</p>";

            var recipientName = toEmail.Split('@')[0];

            var payload = new
            {
                from = sender,
                to = new[] { new { email = toEmail } },
                subject = $"Application {applicationNumber} status updated to {statusCapitalized}",
                text = $"The status of application {applicationNumber} has been updated to {statusCapitalized}.\nComments: {comments ?? "(none)"}\nView: {appLink}",
                html = $"<div style=\"font-family: Arial, sans-serif; color: #222;\">" +
                       $"<h2 style=\"color:#1a73e8;\">Application Status Updated</h2>" +
                       $"<p>Dear {WebUtility.HtmlEncode(recipientName)},</p>" +
                       $"<p>The status of application <strong>{WebUtility.HtmlEncode(applicationNumber)}</strong> submitted by <strong>{WebUtility.HtmlEncode(applicantName)}</strong> has been updated to <strong>{WebUtility.HtmlEncode(statusCapitalized)}</strong>.</p>" +
                       commentsHtml +
                       $"<p>You can view the application here: <a href=\"{appLink}\">{appLink}</a></p>" +
                       $"<p>This is an automated notification from Mortgage Management Portal.</p>" +
                       $"</div>"
            };

            var req = new HttpRequestMessage(HttpMethod.Post, $"{baseUrl}/email/2/send");
            req.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("App", apiKey);
            req.Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

            var res = await _http.SendAsync(req, ct);
            var body = await res.Content.ReadAsStringAsync(ct);

            if (!res.IsSuccessStatusCode)
            {
                _logger.LogError("Infobip send failed: {Status} {Body}", res.StatusCode, body);
                throw new Exception($"Infobip send failed: {res.StatusCode}");
            }

            _logger.LogInformation("Infobip accepted status update notification for {Email}", toEmail);
        }
    }
}