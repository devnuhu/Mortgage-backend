using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using MMGT.API.Data;
using MMGT.API.Models;
using MMGT.API.Services.Interfaces;

namespace MMGT.API.Services
{
    public class StepDownNotifyResult
    {
        public List<string> EmailFailures { get; } = new();
        public string? SmsOutcome { get; set; }   // "queued", "not sent: ..." or null when no SMS was due
        public int EmailsSent { get; set; }
    }

    public class EmailTestResult
    {
        public bool Ok { get; set; }
        public string Message { get; set; } = string.Empty;
        public Dictionary<string, string> Settings { get; set; } = new();
        public int? InfobipStatus { get; set; }
        public string? InfobipResponse { get; set; }
    }
    public interface IStepDownNotifier
    {
        Task<StepDownNotifyResult> NotifyAsync(string action, ApplicationStepDownRequest r);
        Task<EmailTestResult> SendTestAsync(string toEmail);
    }

    /// <summary>
    /// Emails for the step-down workflow and one SMS to the borrower when an application is stepped down.
    /// Master switch: StepDown:NotificationsEnabled (default false). When off, it only logs what it would have sent.
    /// Email uses the same Infobip settings as the PeopleHub set-up (EmailSettings:*, INFOBIP_API_KEY), otherwise it skips quietly.
    /// </summary>
    public class StepDownNotifier : IStepDownNotifier
    {
        private readonly HttpClient _http;
        private readonly IConfiguration _config;
        private readonly ApplicationDbContext _db;
        private readonly ISmsService _sms;
        private readonly ILogger<StepDownNotifier> _logger;

        public StepDownNotifier(HttpClient http, IConfiguration config, ApplicationDbContext db, ISmsService sms, ILogger<StepDownNotifier> logger)
        {
            _http = http; _config = config; _db = db; _sms = sms; _logger = logger;
        }

        private sealed record Person(Guid Id, string Email, string Name, string Role = "");

        public async Task<StepDownNotifyResult> NotifyAsync(string action, ApplicationStepDownRequest r)
        {
            var result = new StepDownNotifyResult();

            var app = await _db.Applications.IgnoreQueryFilters().AsNoTracking()
                .Where(a => a.Id == r.ApplicationId)
                .Select(a => new { a.ApplicationNumber, a.PhoneNumber, a.PmiCompanyName, a.RsaPin, Borrower = a.FirstName + " " + a.LastName })
                .FirstOrDefaultAsync();
            var appNo = app?.ApplicationNumber ?? r.ApplicationId.ToString();
            var borrower = app?.Borrower?.Trim() ?? string.Empty;
            var rsaPin = app?.RsaPin?.Trim() ?? string.Empty;
            var pmiName = string.IsNullOrWhiteSpace(app?.PmiCompanyName) ? (r.PmiCompanyName ?? string.Empty) : app!.PmiCompanyName.Trim();

            // ---- who gets an email ----
            var people = new List<Person>();
            async Task AddRoleAsync(string role)
            {
                var rows = await _db.Users.AsNoTracking()
                    .Where(u => u.Role == role && u.IsActive && u.Email != null && u.Email != "")
                    .Select(u => new { u.Id, u.Email, u.FirstName, u.LastName }).ToListAsync();
                people.AddRange(rows.Select(u => new Person(u.Id, u.Email, (u.FirstName + " " + u.LastName).Trim(), role)));
            }
            async Task AddUserAsync(Guid? id)
            {
                if (id == null) return;
                var u = await _db.Users.AsNoTracking().Where(x => x.Id == id && x.Email != null && x.Email != "")
                    .Select(x => new { x.Id, x.Email, x.FirstName, x.LastName }).FirstOrDefaultAsync();
                if (u != null) people.Add(new Person(u.Id, u.Email, (u.FirstName + " " + u.LastName).Trim()));
            }

            string subject, heading, line;
            switch (action)
            {
                case StepDownAction.Submitted:
                    if (r.RequiresHodApproval) await AddRoleAsync(StepDownRoles.Approver);
                    await AddRoleAsync(StepDownRoles.Checker);
                    subject = $"Step-down request submitted: {appNo}";
                    heading = "Step-down request submitted";
                    line = r.RequiresHodApproval
                        ? $"A step-down request has been submitted for application {appNo} and is pending the decision of the HOD Benefit Admin."
                        : $"A step-down request has been submitted for application {appNo} and is pending review and action by the Mortgage Lead.";
                    break;
                case StepDownAction.Approved:
                    await AddRoleAsync(StepDownRoles.Checker);
                    await AddUserAsync(r.RequestedById);
                    subject = $"Step-down request approved, awaiting action: {appNo}";
                    heading = "Step-down request approved";
                    line = $"The step-down request for application {appNo} has been approved by the HOD Benefit Admin and is pending action by the Mortgage Lead.";
                    break;
                case StepDownAction.Rejected:
                    await AddUserAsync(r.RequestedById);
                    subject = $"Step-down request rejected: {appNo}";
                    heading = "Step-down request rejected";
                    line = $"The step-down request for application {appNo} has been rejected.";
                    break;
                case StepDownAction.CancellationRequested:
                    if (r.DecidedById != null || !r.RequiresHodApproval) await AddRoleAsync(StepDownRoles.Checker); else await AddRoleAsync(StepDownRoles.Approver);
                    subject = $"Request to cancel a step-down: {appNo}";
                    heading = "Request to cancel a step-down";
                    line = $"A request to cancel the step-down request for application {appNo} has been submitted.";
                    break;
                case StepDownAction.CancellationAccepted:
                case StepDownAction.CancellationDeclined:
                    await AddUserAsync(r.RequestedById);
                    var accepted = action == StepDownAction.CancellationAccepted;
                    subject = $"Cancellation {(accepted ? "accepted" : "declined")}: {appNo}";
                    heading = $"Cancellation {(accepted ? "accepted" : "declined")}";
                    line = accepted
                        ? $"The step-down request for application {appNo} has been cancelled."
                        : $"The request to cancel the step-down request for application {appNo} has been declined.";
                    break;
                case StepDownAction.SteppedDown:
                    await AddUserAsync(r.RequestedById);
                    await AddUserAsync(r.DecidedById);
                    subject = $"Application stepped down: {appNo}";
                    heading = "Application stepped down";
                    line = $"Application {appNo} has been stepped down.";
                    break;
                case StepDownAction.Restored:
                    await AddUserAsync(r.RequestedById);
                    await AddUserAsync(r.DecidedById);
                    subject = $"Application restored: {appNo}";
                    heading = "Application restored";
                    line = $"Application {appNo} has been restored.";
                    break;
                default:
                    return result; // other actions (draft created, draft removed...) send nothing
            }

            var subjectTag = string.IsNullOrWhiteSpace(rsaPin) ? appNo : $"RSA PIN {rsaPin} ({appNo})";
            subject = "Mortgage: " + subject.Replace(appNo, subjectTag);
            var about = string.IsNullOrWhiteSpace(rsaPin) ? appNo : $"RSA PIN {rsaPin}";
            string LineFor(Person p) => action switch
            {
                StepDownAction.Submitted => !r.RequiresHodApproval
                    ? $"A step-down request has been submitted for {about} and is pending your review and action."
                    : p.Role == StepDownRoles.Approver
                    ? $"A step-down request has been submitted for {about} and is pending your approval."
                    : $"A step-down request has been submitted for {about} and is pending approval by the HOD Benefit Admin.",
                StepDownAction.Approved => p.Role == StepDownRoles.Checker
                    ? $"The step-down request for {about} has been approved by the HOD Benefit Admin and is pending your action."
                    : $"The step-down request for {about} has been approved by the HOD Benefit Admin and is pending action by the Mortgage Lead.",
                StepDownAction.Rejected => $"The step-down request for {about} has been rejected.",
                StepDownAction.CancellationRequested => $"A request to cancel the step-down request for {about} is pending your decision.",
                StepDownAction.CancellationAccepted => $"The step-down request for {about} has been cancelled.",
                StepDownAction.CancellationDeclined => $"The request to cancel the step-down request for {about} has been declined.",
                StepDownAction.SteppedDown => $"The application with {about} has been stepped down.",
                StepDownAction.Restored => $"The application with {about} has been restored.",
                _ => line
            };
            // never email the same person twice
            people = people.GroupBy(p => p.Email.ToLowerInvariant()).Select(g => g.First()).ToList();

            var enabled = string.Equals(_config["StepDown:NotificationsEnabled"], "true", StringComparison.OrdinalIgnoreCase);
            var frontend = (_config["FRONTEND_URL"] ?? "http://localhost:5173").TrimEnd('/');
            var link = $"{frontend}/stepdown/requests/{r.Id}";
            var facts = new List<(string Label, string Value)>
            {
                ("Borrower", borrower), ("RSA PIN", rsaPin), ("Application number", appNo), ("PMI", pmiName)
            };

            foreach (var p in people)
            {
                if (!enabled)
                {
                    _logger.LogInformation("[STEPDOWN-NOTIFY-OFF] Would email {Name} <{Email}>: {Subject}", p.Name, p.Email, subject);
                    continue;
                }
                try
                {
                    var sent = await SendEmailAsync(p, subject, heading, LineFor(p), link, r, facts);
                    if (sent) result.EmailsSent++;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "[STEPDOWN-NOTIFY] Email to {Email} failed for request {Id}", p.Email, r.Id);
                    result.EmailFailures.Add($"{p.Email}: {ex.Message}");
                }
            }

            // ---- SMS to the borrower: ONLY when stepped down ----
            if (action == StepDownAction.SteppedDown)
            {
                var pmi = string.IsNullOrWhiteSpace(app?.PmiCompanyName) ? r.PmiCompanyName : app!.PmiCompanyName;
                const string generic = "Dear Client, your mortgage application has been stepped down.";
                var msg = generic;
                if (!string.IsNullOrWhiteSpace(pmi))
                {
                    // One SMS segment = 160 plain characters (70 if any accent or special character is used). Longer text is split and billed as several.
                    var withPmi = $"Dear Client, your mortgage application with {pmi.Trim()} has been stepped down.";
                    if (withPmi.Length <= 160 && withPmi.All(ch => ch >= 32 && ch < 127)) msg = withPmi;
                }
                if (!enabled)
                {
                    _logger.LogInformation("[STEPDOWN-NOTIFY-OFF] Would SMS borrower of {AppNo}: {Msg}", appNo, msg);
                }
                else if (string.IsNullOrWhiteSpace(app?.PhoneNumber))
                {
                    result.SmsOutcome = "not sent: the application has no phone number";
                }
                else
                {
                    try
                    {
                        var ok = await _sms.SendSmsAsync(app!.PhoneNumber!, msg, appNo);
                        result.SmsOutcome = ok ? "queued" : "not sent: the phone number is not valid or the SMS queue refused it";
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "[STEPDOWN-NOTIFY] SMS failed for {AppNo}", appNo);
                        result.SmsOutcome = "not sent: " + ex.Message;
                    }
                }
            }
            return result;
        }

        // Infobip's email endpoint takes a multipart form, not JSON.
        private static MultipartFormDataContent BuildForm(string from, string to, string subject, string text, string html)
        {
            var form = new MultipartFormDataContent();
            form.Add(new StringContent(from ?? string.Empty, Encoding.UTF8), "from");
            form.Add(new StringContent(to ?? string.Empty, Encoding.UTF8), "to");
            form.Add(new StringContent(subject ?? string.Empty, Encoding.UTF8), "subject");
            form.Add(new StringContent(html ?? string.Empty, Encoding.UTF8), "html");
            form.Add(new StringContent(text ?? string.Empty, Encoding.UTF8), "text");
            return form;
        }
        public async Task<EmailTestResult> SendTestAsync(string toEmail)
        {
            var res = new EmailTestResult();
            var section = _config.GetSection("EmailSettings:Infobip");
            string First(params string?[] v) => v.FirstOrDefault(x => !string.IsNullOrWhiteSpace(x))?.Trim() ?? string.Empty;
            var provider = _config["EmailSettings:Provider"];
            var baseUrl = First(section["BaseUrl"], _config["INFOBIP_BASE_URL"])
                .Replace("https://", string.Empty, StringComparison.OrdinalIgnoreCase)
                .Replace("http://", string.Empty, StringComparison.OrdinalIgnoreCase).TrimEnd('/');
            var senderEmail = First(section["SenderEmail"], _config["INFOBIP_SENDER"]);
            var senderName = First(section["SenderName"], "Mortgage Management Portal");
            var apiKey = First(_config["INFOBIP_API_KEY"], section["ApiKey"]);
            var frontend = _config["FRONTEND_URL"];

            res.Settings["EmailSettings:Provider"] = string.IsNullOrWhiteSpace(provider) ? "(not set, treated as on)" : provider!;
            res.Settings["Base URL"] = string.IsNullOrWhiteSpace(baseUrl) ? "MISSING" : baseUrl;
            res.Settings["Sender email"] = string.IsNullOrWhiteSpace(senderEmail) ? "MISSING" : senderEmail;
            res.Settings["Sender name"] = senderName;
            res.Settings["INFOBIP_API_KEY"] = string.IsNullOrWhiteSpace(apiKey) ? "MISSING" : $"set ({apiKey.Length} characters)";
            res.Settings["FRONTEND_URL"] = string.IsNullOrWhiteSpace(frontend) ? "(not set, links use http://localhost:5173)" : frontend!;
            res.Settings["StepDown:NotificationsEnabled"] = _config["StepDown:NotificationsEnabled"] ?? "(not set = OFF, workflow emails will not send)";

            if (string.Equals(provider, "Off", StringComparison.OrdinalIgnoreCase))
            {
                res.Message = "EmailSettings:Provider is Off, so nothing is sent.";
                return res;
            }
            var missing = new List<string>();
            if (string.IsNullOrWhiteSpace(baseUrl)) missing.Add("base URL");
            if (string.IsNullOrWhiteSpace(senderEmail)) missing.Add("sender email");
            if (string.IsNullOrWhiteSpace(apiKey)) missing.Add("API key");
            if (missing.Count > 0)
            {
                res.Message = "Not sent. Missing: " + string.Join(", ", missing) + ". If you set them as Windows environment variables, close and reopen Visual Studio (on a server, recycle the app pool).";
                return res;
            }

            try
            {
                var payload = new
                {
                    from = senderName + " <" + senderEmail + ">",
                    to = new[] { new { email = toEmail } },
                    subject = "Mortgage portal: email test",
                    text = "This is a test email from the Mortgage portal. If you can read this, Infobip email is working.",
                    html = "<div style=\"font-family: Arial, sans-serif; color: #222;\"><h2 style=\"color:#1a73e8;\">Email test</h2><p>This is a test email from the Mortgage portal. If you can read this, Infobip email is working.</p></div>"
                };
                using var req = new HttpRequestMessage(HttpMethod.Post, $"https://{baseUrl}/email/3/send");
                req.Headers.Authorization = new AuthenticationHeaderValue("App", apiKey);
                req.Content = BuildForm(payload.from, payload.to[0].email, payload.subject, payload.text, payload.html);
                using var resp = await _http.SendAsync(req);
                var body = await resp.Content.ReadAsStringAsync();
                res.InfobipStatus = (int)resp.StatusCode;
                res.InfobipResponse = body.Length > 600 ? body.Substring(0, 600) + "..." : body;
                res.Ok = resp.IsSuccessStatusCode;
                res.Message = res.Ok
                    ? "Infobip accepted the email. Check the inbox (and spam). If it never arrives, the sender domain is probably not verified in Infobip."
                    : "Infobip refused the email. 401 = wrong API key. 403 = the key has no permission to send email. 404 or a connection error = wrong base URL. 400 = sender address or domain not allowed.";
            }
            catch (Exception ex)
            {
                res.Message = "Could not reach Infobip: " + ex.Message + " (check the base URL and that this PC or server can reach the internet).";
            }
            return res;
        }
        private async Task<bool> SendEmailAsync(Person to, string subject, string heading, string line, string link, ApplicationStepDownRequest r, List<(string Label, string Value)> facts)
        {
            // Same settings as the PeopleHub email set-up: EmailSettings:Provider (Off switches email off),
            // EmailSettings:Infobip:BaseUrl / SenderEmail / SenderName, and the API key from INFOBIP_API_KEY (environment variable).
            // The older INFOBIP_BASE_URL and INFOBIP_SENDER settings still work as fallbacks.
            if (string.Equals(_config["EmailSettings:Provider"], "Off", StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogInformation("Email sending is switched off (EmailSettings:Provider). Step-down email not sent.");
                return false;
            }
            var section = _config.GetSection("EmailSettings:Infobip");
            string First(params string?[] v) => v.FirstOrDefault(x => !string.IsNullOrWhiteSpace(x))?.Trim() ?? string.Empty;
            var baseUrl = First(section["BaseUrl"], _config["INFOBIP_BASE_URL"])
                .Replace("https://", string.Empty, StringComparison.OrdinalIgnoreCase)
                .Replace("http://", string.Empty, StringComparison.OrdinalIgnoreCase).TrimEnd('/');
            var senderEmail = First(section["SenderEmail"], _config["INFOBIP_SENDER"]);
            var senderName = First(section["SenderName"], "Mortgage Management Portal");
            var apiKey = First(_config["INFOBIP_API_KEY"], section["ApiKey"]);
            if (string.IsNullOrWhiteSpace(baseUrl) || string.IsNullOrWhiteSpace(apiKey) || string.IsNullOrWhiteSpace(senderEmail))
            {
                _logger.LogWarning("Step-down email not sent: Infobip is not set up (base URL, API key or sender address is missing).");
                return false;
            }
            var sender = senderName + " <" + senderEmail + ">";

            var enc = (string? s) => WebUtility.HtmlEncode(s ?? string.Empty);
            var shown = facts.Where(f => !string.IsNullOrWhiteSpace(f.Value)).ToList();
            var factsHtml = shown.Count == 0 ? "" :
                "<table role=\"presentation\" cellspacing=\"0\" cellpadding=\"0\" style=\"margin:12px 0;border-collapse:collapse;\">" +
                string.Concat(shown.Select(f => $"<tr><td style=\"padding:4px 16px 4px 0;color:#6B7684;\">{enc(f.Label)}</td><td style=\"padding:4px 0;\"><strong>{enc(f.Value)}</strong></td></tr>")) +
                "</table>";
            var factsText = string.Join("\n", shown.Select(f => f.Label + ": " + f.Value));
            var reason = string.IsNullOrWhiteSpace(r.Reason) ? "" : $"<p><strong>Reason given:</strong> {enc(r.Reason)}</p>";
            var payload = new
            {
                from = sender,
                to = new[] { new { email = to.Email } },
                subject,
                text = $"{line}\n{factsText}\nOpen the request: {link}\nThis is an automated notification from Mortgage Management Portal.",
                html = "<div style=\"font-family: Arial, sans-serif; color: #222;\">" +
                       $"<h2 style=\"color:#1a73e8;\">{enc(heading)}</h2>" +
                       $"<p>Dear {enc(string.IsNullOrWhiteSpace(to.Name) ? "colleague" : to.Name)},</p>" +
                       $"<p>{enc(line)}</p>" + factsHtml + reason +
                       $"<p><a href=\"{link}\">Open the request</a></p>" +
                       "<p>This is an automated notification from Mortgage Management Portal.</p></div>"
            };

            using var req = new HttpRequestMessage(HttpMethod.Post, $"https://{baseUrl}/email/3/send");
            req.Headers.Authorization = new AuthenticationHeaderValue("App", apiKey);
            req.Content = BuildForm(payload.from, payload.to[0].email, payload.subject, payload.text, payload.html);
            var res = await _http.SendAsync(req);
            if (!res.IsSuccessStatusCode)
            {
                var body = await res.Content.ReadAsStringAsync();
                _logger.LogError("Infobip step-down send failed: {Status} {Body}", res.StatusCode, body);
                throw new Exception($"Infobip send failed: {res.StatusCode}");
            }
            _logger.LogInformation("Step-down email accepted for {Email}: {Subject}", to.Email, subject);
            return true;
        }
    }
}
