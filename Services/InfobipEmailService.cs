using System.Net.Mail;
using System.Text;
using System.Text.Json;
using MMGT.API.Services.Email;
using MMGT.API.Services.Interfaces;

namespace MMGT.API.Services
{
    /// <summary>
    /// Sends the portal's emails through Infobip, in the same way as the PeopleHub application.
    /// Settings: EmailSettings:Infobip:BaseUrl, SenderEmail and SenderName; the API key comes from the INFOBIP_API_KEY
    /// setting (an environment variable on the server) and is never written to a file or a log.
    /// The older INFOBIP_BASE_URL and INFOBIP_SENDER settings still work as fallbacks.
    /// Sending never throws: a failed email is logged and the action that asked for it carries on.
    /// </summary>
    public class InfobipEmailService : IEmailService
    {
        private readonly HttpClient _http;
        private readonly ILogger<InfobipEmailService> _logger;
        private readonly string _baseUrl;
        private readonly string _senderEmail;
        private readonly string _senderName;
        private readonly string _apiKey;
        private readonly string _frontend;

        public InfobipEmailService(HttpClient http, IConfiguration config, ILogger<InfobipEmailService> logger)
        {
            _http = http;
            _logger = logger;

            var section = config.GetSection("EmailSettings:Infobip");
            var baseUrl = First(section["BaseUrl"], config["INFOBIP_BASE_URL"]);
            baseUrl = baseUrl.Replace("https://", string.Empty, StringComparison.OrdinalIgnoreCase)
                             .Replace("http://", string.Empty, StringComparison.OrdinalIgnoreCase)
                             .TrimEnd('/');
            _baseUrl = baseUrl;
            _senderEmail = First(section["SenderEmail"], config["INFOBIP_SENDER"]);
            _senderName = First(section["SenderName"], EmailBrand.Current.TeamName);
            _apiKey = First(config["INFOBIP_API_KEY"], section["ApiKey"]);
            _frontend = First(config["FRONTEND_URL"], "http://localhost:5173").TrimEnd('/');
        }

        private static string First(params string?[] values)
        {
            foreach (var v in values)
                if (!string.IsNullOrWhiteSpace(v)) return v.Trim();
            return string.Empty;
        }

        public Task SendFirstLoginLinkAsync(Guid userId, string toEmail, string token, CancellationToken ct = default)
        {
            var brand = EmailBrand.Current;
            var link = $"{_frontend}/first-login?uid={userId}&token={Uri.EscapeDataString(token)}";
            var content = new EmailContent
            {
                Subject = "Complete your account setup",
                Heading = "Set up your account",
                Preheader = "Choose your password to start using the portal",
                Greeting = "Hello,",
                Automatic = true,
                Blocks = new List<EmailBlock>
                {
                    EmailBlock.Paragraph($"An account has been created for you on the **{brand.AppName}**. Use the button below to choose your password."),
                    EmailBlock.Button("Set my password", link),
                    EmailBlock.Callout("This link expires in 2 hours. If it has expired, ask your administrator to send you a new one.", "warning"),
                    EmailBlock.Note("If you were not expecting this email, you can ignore it.")
                }
            };
            return SendAsync(toEmail, content, ct);
        }

        public Task SendApplicationCreatedNotificationAsync(string toEmail, Guid applicationId, string applicationNumber, string applicantName, CancellationToken ct = default)
        {
            var appLink = $"{_frontend}/applications/{applicationId}";
            var content = new EmailContent
            {
                Subject = $"New application received: {applicationNumber}",
                Heading = "New application received",
                Preheader = $"Application {applicationNumber} from {applicantName}",
                Greeting = "Dear Partner,",
                Automatic = true,
                Blocks = new List<EmailBlock>
                {
                    EmailBlock.Paragraph("A new application has been submitted and is ready for your review."),
                    EmailBlock.Details(("Application number", applicationNumber), ("Applicant", applicantName)),
                    EmailBlock.Button("View application", appLink)
                }
            };
            return SendAsync(toEmail, content, ct);
        }

        public Task SendApplicationRejectedAsync(string toEmail, Guid applicationId, string applicationNumber, string rsaPin, string applicantName, string? reason, IReadOnlyList<(string Name, string? Reason)>? rejectedDocuments = null, CancellationToken ct = default)
        {
            var appLink = $"{_frontend}/applications/{applicationId}";

            var blocks = new List<EmailBlock>
            {
                EmailBlock.Paragraph($"We are sorry to tell you that the application for RSA PIN **{rsaPin}** was not accepted at this stage. Please review the details below."),
                EmailBlock.Details(("RSA PIN", rsaPin), ("Applicant", applicantName), ("Application number", applicationNumber), ("Status", "Rejected"))
            };
            blocks.Add(EmailBlock.Section("Reason given by the reviewer"));
            if (!string.IsNullOrWhiteSpace(reason))
                blocks.Add(EmailBlock.Callout(reason.Trim(), "danger"));
            else
                blocks.Add(EmailBlock.Note("No reason was recorded. Please open the application for details."));
            if (rejectedDocuments != null && rejectedDocuments.Count > 0)
            {
                blocks.Add(EmailBlock.Section(rejectedDocuments.Count == 1 ? "Document to replace" : $"{rejectedDocuments.Count} documents to replace"));
                var number = 0;
                foreach (var rejectedDoc in rejectedDocuments)
                {
                    number++;
                    var title = rejectedDocuments.Count > 1 ? $"{number}. {rejectedDoc.Name}" : rejectedDoc.Name;
                    blocks.Add(EmailBlock.Item(title, string.IsNullOrWhiteSpace(rejectedDoc.Reason) ? "No reason was recorded for this document." : rejectedDoc.Reason.Trim(), "danger"));
                }
            }
            blocks.Add(EmailBlock.Paragraph("You can correct the application, upload the missing or corrected documents and send it again for review."));
            blocks.Add(EmailBlock.Button("Open application", appLink));

            var content = new EmailContent
            {
                Subject = $"Application for RSA PIN {rsaPin} was rejected",
                Heading = "Application rejected",
                Badge = "Rejected",
                BadgeTone = "danger",
                Reference = $"Application {applicationNumber}",
                Preheader = $"The application for RSA PIN {rsaPin} needs your attention",
                Greeting = $"Dear {EmailLayout.NameFromEmail(toEmail)},",
                Automatic = true,
                Blocks = blocks
            };
            return SendAsync(toEmail ?? string.Empty, content, ct);
        }

        public Task SendApplicationResubmittedAsync(string toEmail, Guid applicationId, string applicationNumber, string rsaPin, string applicantName, int resubmissionCount, string? note, CancellationToken ct = default)
        {
            var queueLink = $"{_frontend}/admin/my-queue";

            var blocks = new List<EmailBlock>
            {
                EmailBlock.Paragraph($"The client has corrected and resubmitted the application for RSA PIN **{rsaPin}**. It is back in your queue and waiting for your review."),
                EmailBlock.Details(("RSA PIN", rsaPin), ("Applicant", applicantName), ("Application number", applicationNumber), ("Times resubmitted", resubmissionCount.ToString()))
            };
            if (!string.IsNullOrWhiteSpace(note))
            {
                blocks.Add(EmailBlock.Section("Note from the client"));
                blocks.Add(EmailBlock.Callout(note.Trim(), "info"));
            }
            else
                blocks.Add(EmailBlock.Note("The client did not leave a note. Check the replaced documents in the application."));
            blocks.Add(EmailBlock.Button("Open My Queue", queueLink));

            var content = new EmailContent
            {
                Subject = $"Resubmitted: application {applicationNumber} (RSA PIN {rsaPin})",
                Heading = "Application resubmitted",
                Badge = "Resubmitted",
                BadgeTone = "info",
                Reference = $"Application {applicationNumber}",
                Preheader = $"{applicantName} resubmitted application {applicationNumber}",
                Greeting = $"Dear {EmailLayout.NameFromEmail(toEmail)},",
                Automatic = true,
                Blocks = blocks
            };
            return SendAsync(toEmail ?? string.Empty, content, ct);
        }

        public Task SendApplicationStatusUpdatedAsync(string toEmail, Guid applicationId, string applicationNumber, string applicantName, string newStatus, string? comments, CancellationToken ct = default)
        {
            var appLink = $"{_frontend}/applications/{applicationId}";
            var status = string.IsNullOrEmpty(newStatus) ? newStatus : char.ToUpperInvariant(newStatus[0]) + newStatus.Substring(1);
            var recipientName = (toEmail ?? string.Empty).Split('@')[0];

            var blocks = new List<EmailBlock>
            {
                EmailBlock.Paragraph($"The status of application **{applicationNumber}** has been updated."),
                EmailBlock.Details(("Application number", applicationNumber), ("Applicant", applicantName), ("New status", status), ("Comments", comments ?? string.Empty))
            };
            if (string.IsNullOrWhiteSpace(comments))
                blocks.Add(EmailBlock.Note("No additional comments were provided."));
            blocks.Add(EmailBlock.Button("View application", appLink));

            var content = new EmailContent
            {
                Subject = $"Application {applicationNumber} status updated to {status}",
                Heading = "Application status updated",
                Preheader = $"Application {applicationNumber} is now {status}",
                Greeting = $"Dear {recipientName},",
                Automatic = true,
                Blocks = blocks
            };
            return SendAsync(toEmail ?? string.Empty, content, ct);
        }

        private async Task SendAsync(string to, EmailContent content, CancellationToken ct)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(_apiKey) || string.IsNullOrWhiteSpace(_baseUrl) || string.IsNullOrWhiteSpace(_senderEmail))
                {
                    _logger.LogWarning("Email not sent: Infobip is not set up (base URL, API key or sender address is missing).");
                    return;
                }

                var recipient = (to ?? string.Empty).Trim();
                if (recipient.Length == 0 || !MailAddress.TryCreate(recipient, out _))
                {
                    _logger.LogWarning("Email not sent: the recipient address is not valid.");
                    return;
                }

                using var form = new MultipartFormDataContent();
                form.Add(new StringContent(_senderName + " <" + _senderEmail + ">", Encoding.UTF8), "from");
                form.Add(new StringContent(recipient, Encoding.UTF8), "to");
                form.Add(new StringContent(content.Subject ?? string.Empty, Encoding.UTF8), "subject");
                form.Add(new StringContent(EmailLayout.RenderText(content), Encoding.UTF8), "text");
                form.Add(new StringContent(EmailLayout.RenderHtml(content), Encoding.UTF8), "html");

                using var request = new HttpRequestMessage(HttpMethod.Post, "https://" + _baseUrl + "/email/2/send");
                request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("App", _apiKey);
                request.Content = form;

                using var response = await _http.SendAsync(request, ct);
                var body = await response.Content.ReadAsStringAsync(ct);

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning("Infobip refused the email to {Email} ({Status}): {Detail}", recipient, (int)response.StatusCode, Shorten(body));
                    return;
                }

                _logger.LogInformation("Email accepted by Infobip for {Email} with subject '{Subject}'", recipient, content.Subject);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Sending an email through Infobip failed");
            }
        }

        private static string Shorten(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return "no details";
            var flat = text.Replace("\r", " ").Replace("\n", " ").Trim();
            return flat.Length <= 300 ? flat : flat.Substring(0, 300) + "...";
        }
    }
}