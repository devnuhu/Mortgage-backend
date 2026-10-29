using System.Text.RegularExpressions;

namespace MMGT.API.Services.Email
{
    /// <summary>
    /// How the emails look and who they come from. Read once at start-up from EmailSettings:Brand
    /// (company name, logo address, colours). Colours and addresses are checked, so a bad
    /// setting falls back to the default instead of breaking the layout.
    /// </summary>
    public sealed class EmailBrand
    {
        /// <summary>The name used in the header and in sentences ("Access Pensions").</summary>
        public string CompanyName { get; init; } = "Access Pensions";

        /// <summary>The full legal name used in the footer.</summary>
        public string LegalName { get; init; } = "Access Pensions Plc";

        /// <summary>The team that signs the emails and appears as the sender name.</summary>
        public string TeamName { get; init; } = "Access Mortgage";

        /// <summary>The name of the application, shown in the header and in the footer of system messages.</summary>
        public string AppName { get; init; } = "Mortgage Management Portal";

        /// <summary>Public address of the logo image. Empty means a text wordmark is shown.</summary>
        public string LogoUrl { get; init; } = "https://accesspensions.ng/wp-content/uploads/2021/09/access_logo.png";

        /// <summary>Header text, headings, buttons and links. Company blue.</summary>
        public string PrimaryColor { get; init; } = "#003883";

        /// <summary>Orange part of the colour stripe under the header.</summary>
        public string AccentColor { get; init; } = "#FF8200";

        /// <summary>Green header band.</summary>
        public string SecondaryColor { get; init; } = "#8DC63F";

        public string SupportEmail { get; init; } = "info@accesspensions.ng";

        /// <summary>The brand used by every email. Set once at start-up; the defaults apply until then.</summary>
        public static EmailBrand Current { get; set; } = new EmailBrand();

        private static readonly Regex Colour = new Regex("^#[0-9a-fA-F]{6}$", RegexOptions.Compiled);

        public static EmailBrand FromConfiguration(IConfiguration configuration)
        {
            var section = configuration.GetSection("EmailSettings:Brand");
            var d = new EmailBrand();

            string Text(string key, string fallback)
            {
                var value = section[key];
                return string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
            }
            string Hex(string key, string fallback)
            {
                var value = section[key];
                return !string.IsNullOrWhiteSpace(value) && Colour.IsMatch(value.Trim()) ? value.Trim() : fallback;
            }
            string Url(string key, string fallback)
            {
                var value = section[key];
                if (string.IsNullOrWhiteSpace(value)) return fallback;
                return Uri.TryCreate(value.Trim(), UriKind.Absolute, out var uri) && (uri.Scheme == Uri.UriSchemeHttps || uri.Scheme == Uri.UriSchemeHttp)
                    ? uri.ToString() : fallback;
            }

            return new EmailBrand
            {
                CompanyName = Text("CompanyName", d.CompanyName),
                LegalName = Text("LegalName", d.LegalName),
                TeamName = Text("TeamName", d.TeamName),
                AppName = Text("AppName", d.AppName),
                LogoUrl = Url("LogoUrl", d.LogoUrl),
                PrimaryColor = Hex("PrimaryColor", d.PrimaryColor),
                AccentColor = Hex("AccentColor", d.AccentColor),
                SecondaryColor = Hex("SecondaryColor", d.SecondaryColor),
                SupportEmail = Text("SupportEmail", d.SupportEmail)
            };
        }
    }
}