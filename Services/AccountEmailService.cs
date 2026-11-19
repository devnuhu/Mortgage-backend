using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using MMGT.API.Services.Email;

namespace MMGT.API.Services
{
    public enum AccountEmailReason { NewAccount, AdminReset, ForgotPassword }

    public interface IAccountEmailService
    {
        /// <summary>True when account emails are switched on (AccountEmails:Enabled) and Infobip is fully set up.</summary>
        bool CanSend();
        Task<bool> SendTemporaryPasswordAsync(string toEmail, string firstName, string temporaryPassword, AccountEmailReason reason);
        Task<bool> SendPasswordChangedAsync(string toEmail, string firstName);
    }

    /// <summary>Cryptographically random temporary passwords: 12 characters, no look-alike characters.</summary>
    public static class TemporaryPasswordGenerator
    {
        private const string Upper = "ABCDEFGHJKMNPQRSTUVWXYZ";
        private const string Lower = "abcdefghijkmnpqrstuvwxyz";
        private const string Digits = "23456789";
        private const string Special = "!@#$%&?";

        public static string Generate(int length = 12)
        {
            var chars = new List<char> { Pick(Upper), Pick(Lower), Pick(Digits), Pick(Special) };
            const string all = Upper + Lower + Digits + Special;
            while (chars.Count < length) chars.Add(Pick(all));
            for (int i = chars.Count - 1; i > 0; i--)
            {
                int j = RandomNumberGenerator.GetInt32(i + 1);
                (chars[i], chars[j]) = (chars[j], chars[i]);
            }
            return new string(chars.ToArray());
        }

        private static char Pick(string s) => s[RandomNumberGenerator.GetInt32(s.Length)];
    }

    /// <summary>
    /// Emails about accounts and passwords. Never throws and never writes a password to a log.
    /// Switch: AccountEmails:Enabled (default off). Infobip settings: EmailSettings:Infobip (BaseUrl, SenderEmail, SenderName) and INFOBIP_API_KEY.
    /// </summary>
    public class AccountEmailService : IAccountEmailService
    {
        private readonly HttpClient _http;
        private readonly IConfiguration _config;
        private readonly ILogger<AccountEmailService> _logger;

        public AccountEmailService(HttpClient http, IConfiguration config, ILogger<AccountEmailService> logger)
        {
            _http = http; _config = config; _logger = logger;
        }

        private static string First(params string?[] v) => v.FirstOrDefault(x => !string.IsNullOrWhiteSpace(x))?.Trim() ?? string.Empty;

        private (string baseUrl, string senderEmail, string senderName, string apiKey) Settings()
        {
            var section = _config.GetSection("EmailSettings:Infobip");
            var baseUrl = First(section["BaseUrl"], _config["INFOBIP_BASE_URL"])
                .Replace("https://", string.Empty, StringComparison.OrdinalIgnoreCase)
                .Replace("http://", string.Empty, StringComparison.OrdinalIgnoreCase).TrimEnd('/');
            return (baseUrl, First(section["SenderEmail"], _config["INFOBIP_SENDER"]),
                    First(section["SenderName"], "Mortgage Management Portal"),
                    First(_config["INFOBIP_API_KEY"], section["ApiKey"]));
        }

        public bool CanSend()
        {
            if (!string.Equals(_config["AccountEmails:Enabled"], "true", StringComparison.OrdinalIgnoreCase)) return false;
            if (string.Equals(_config["EmailSettings:Provider"], "Off", StringComparison.OrdinalIgnoreCase)) return false;
            var s = Settings();
            return s.baseUrl.Length > 0 && s.senderEmail.Length > 0 && s.apiKey.Length > 0;
        }

        public Task<bool> SendTemporaryPasswordAsync(string toEmail, string firstName, string temporaryPassword, AccountEmailReason reason)
        {
            var (subject, heading, intro) = reason switch
            {
                AccountEmailReason.NewAccount => ("Mortgage Portal: your account has been created", "Your account has been created", "An account has been created for you on the Mortgage Portal."),
                AccountEmailReason.AdminReset => ("Mortgage Portal: your password has been reset", "Your password has been reset", "Your password was reset by an administrator."),
                _ => ("Mortgage Portal: your temporary password", "Your temporary password", "A password reset was requested for your account. If you did not make this request, please contact support at " + EmailBrand.Current.SupportEmail + ".")
            };
            var name = string.IsNullOrWhiteSpace(firstName) ? "colleague" : firstName.Trim();
            var portal = (_config["FRONTEND_URL"] ?? string.Empty).Trim().TrimEnd('/');
            string E(string? s) => WebUtility.HtmlEncode(s ?? string.Empty);

            var html = "<div style=\"font-family: Arial, sans-serif; color: #222; max-width: 560px;\">" +
                $"<h2 style=\"color:#1a73e8;\">{E(heading)}</h2>" +
                $"<p>Dear {E(name)},</p><p>{E(intro)}</p>" +
                "<table role=\"presentation\" cellspacing=\"0\" cellpadding=\"0\" style=\"margin:12px 0;border-collapse:collapse;\">" +
                $"<tr><td style=\"padding:4px 16px 4px 0;color:#6B7684;\">Sign-in email</td><td style=\"padding:4px 0;\"><strong>{E(toEmail)}</strong></td></tr>" +
                $"<tr><td style=\"padding:4px 16px 4px 0;color:#6B7684;\">Temporary password</td><td style=\"padding:4px 0;\"><strong style=\"font-family:Consolas,monospace;font-size:16px;letter-spacing:1px;\">{E(temporaryPassword)}</strong></td></tr>" +
                "</table>" +
                "<p>You will be asked to choose a new password when you sign in. Please do not share this password with anyone.</p>" +
                "<p>Need help? Contact <a href=\"mailto:" + E(EmailBrand.Current.SupportEmail) + "\">" + E(EmailBrand.Current.SupportEmail) + "</a>.</p>" +
                (portal.Length > 0 ? $"<p><a href=\"{E(portal)}\">Sign in to the Mortgage Portal</a></p>" : string.Empty) +
                "<p style=\"color:#6B7684;\">This is an automated notification from Mortgage Management Portal.</p></div>";
            var text = $"Dear {name},\n\n{intro}\n\nSign-in email: {toEmail}\nTemporary password: {temporaryPassword}\n\n" +
                "You will be asked to choose a new password when you sign in. Please do not share this password with anyone.\n" +
                "Need help? Contact " + EmailBrand.Current.SupportEmail + "\n" +
                (portal.Length > 0 ? $"Sign in: {portal}\n" : string.Empty) +
                "\nThis is an automated notification from Mortgage Management Portal.";
            return SendAsync(toEmail, subject, html, text, "temporary password (" + reason + ")");
        }

        public Task<bool> SendPasswordChangedAsync(string toEmail, string firstName)
        {
            var name = string.IsNullOrWhiteSpace(firstName) ? "colleague" : firstName.Trim();
            var when = DateTime.UtcNow.AddHours(1).ToString("dd MMM yyyy HH:mm") + " (WAT)";
            string E(string? s) => WebUtility.HtmlEncode(s ?? string.Empty);
            var html = "<div style=\"font-family: Arial, sans-serif; color: #222; max-width: 560px;\">" +
                "<h2 style=\"color:#1a73e8;\">Your password was changed</h2>" +
                $"<p>Dear {E(name)},</p>" +
                $"<p>The password for your Mortgage Portal account was changed on {E(when)}.</p>" +
                "<p>If you did not make this change, please contact support immediately at <a href=\"mailto:" + E(EmailBrand.Current.SupportEmail) + "\">" + E(EmailBrand.Current.SupportEmail) + "</a>.</p>" +
                "<p style=\"color:#6B7684;\">This is an automated notification from Mortgage Management Portal.</p></div>";
            var text = $"Dear {name},\n\nThe password for your Mortgage Portal account was changed on {when}.\n" +
                "If you did not make this change, please contact support immediately at " + EmailBrand.Current.SupportEmail + ".\n\nThis is an automated notification from Mortgage Management Portal.";
            return SendAsync(toEmail, "Mortgage Portal: your password was changed", html, text, "password changed");
        }

        private async Task<bool> SendAsync(string toEmail, string subject, string html, string text, string kind)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(toEmail)) return false;
                if (!CanSend())
                {
                    _logger.LogInformation("[ACCOUNT-EMAIL-OFF] Not sent ({Kind}) to {Email}: account emails are off or Infobip is not fully set up.", kind, toEmail);
                    return false;
                }
                var s = Settings();
                using var form = new MultipartFormDataContent();
                form.Add(new StringContent(s.senderName + " <" + s.senderEmail + ">", Encoding.UTF8), "from");
                form.Add(new StringContent(toEmail, Encoding.UTF8), "to");
                form.Add(new StringContent(subject, Encoding.UTF8), "subject");
                form.Add(new StringContent(html, Encoding.UTF8), "html");
                form.Add(new StringContent(text, Encoding.UTF8), "text");
                using var req = new HttpRequestMessage(HttpMethod.Post, $"https://{s.baseUrl}/email/3/send") { Content = form };
                req.Headers.Authorization = new AuthenticationHeaderValue("App", s.apiKey);
                using var res = await _http.SendAsync(req);
                if (!res.IsSuccessStatusCode)
                {
                    _logger.LogError("[ACCOUNT-EMAIL] Infobip refused the {Kind} email to {Email}: {Status}", kind, toEmail, (int)res.StatusCode);
                    return false;
                }
                _logger.LogInformation("[ACCOUNT-EMAIL] {Kind} email accepted for {Email}", kind, toEmail);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError("[ACCOUNT-EMAIL] {Kind} email to {Email} failed: {Error}", kind, toEmail, ex.GetType().Name);
                return false;
            }
        }
    }
}
