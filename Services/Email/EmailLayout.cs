using System.Net;
using System.Text;
using System.Text.RegularExpressions;

namespace MMGT.API.Services.Email
{
    /// <summary>One piece of an email: a paragraph, a details table, a bullet list, a call-out, a button or a small note.</summary>
    public sealed class EmailBlock
    {
        public string Kind { get; private init; } = "p";
        public string Text { get; private init; } = string.Empty;
        public string Url { get; private init; } = string.Empty;
        public string Tone { get; private init; } = "info";
        public List<KeyValuePair<string, string>> Rows { get; private init; } = new List<KeyValuePair<string, string>>();
        public List<string> Items { get; private init; } = new List<string>();

        /// <summary>A paragraph. Put **words** between double stars to make them bold.</summary>
        public static EmailBlock Paragraph(string text) => new EmailBlock { Kind = "p", Text = text };

        /// <summary>A small grey note.</summary>
        public static EmailBlock Note(string text) => new EmailBlock { Kind = "note", Text = text };

        /// <summary>A box with label and value rows (application number, status, and so on).</summary>
        public static EmailBlock Details(params (string Label, string Value)[] rows)
        {
            var block = new EmailBlock { Kind = "details" };
            foreach (var row in rows)
                if (!string.IsNullOrWhiteSpace(row.Value))
                    block.Rows.Add(new KeyValuePair<string, string>(row.Label, row.Value));
            return block;
        }

        public static EmailBlock Bullets(params string[] items) => new EmailBlock { Kind = "bullets", Items = items.ToList() };

        /// <summary>A highlighted box. Tone: info, success, warning or danger.</summary>
        public static EmailBlock Callout(string text, string tone = "info") => new EmailBlock { Kind = "callout", Text = text, Tone = tone };

        public static EmailBlock Button(string text, string url) => new EmailBlock { Kind = "button", Text = text, Url = url };
    }

    /// <summary>What an email says. The layout turns it into HTML and plain text.</summary>
    public sealed class EmailContent
    {
        public string Subject { get; init; } = string.Empty;
        public string Heading { get; init; } = string.Empty;
        public string Preheader { get; init; } = string.Empty;
        public string Greeting { get; init; } = string.Empty;
        public List<EmailBlock> Blocks { get; init; } = new List<EmailBlock>();

        /// <summary>When true the sign-off reads as an automatic message, not a personal one.</summary>
        public bool Automatic { get; init; }
    }

    /// <summary>
    /// Turns EmailContent into a professional, responsive email: a branded header, a white card, clear typography,
    /// a bulletproof button and a footer. Tables and inline styles only, so it works in Outlook, Gmail and on phones.
    /// Every value is HTML-encoded here, so names and comments can never inject markup.
    /// </summary>
    public static class EmailLayout
    {
        private static readonly Regex Bold = new Regex(@"\*\*(.+?)\*\*", RegexOptions.Compiled);
        private const string Font = "'Segoe UI', Helvetica, Arial, sans-serif";

        private static string Enc(string? value) => WebUtility.HtmlEncode(value ?? string.Empty);

        /// <summary>Encodes the text, then turns **words** into bold and line breaks into line breaks.</summary>
        private static string Rich(string? value)
        {
            var encoded = Enc(value);
            encoded = Bold.Replace(encoded, "<strong>$1</strong>");
            return encoded.Replace("\r\n", "\n").Replace("\n", "<br/>");
        }

        private static string SafeUrl(string url)
        {
            return Uri.TryCreate((url ?? string.Empty).Trim(), UriKind.Absolute, out var uri)
                   && (uri.Scheme == Uri.UriSchemeHttps || uri.Scheme == Uri.UriSchemeHttp)
                ? uri.ToString() : "#";
        }

        private static (string Back, string Edge, string Ink) Tone(EmailBrand brand, string tone)
        {
            switch (tone)
            {
                case "success": return ("#ECF8F1", "#2E9D63", "#1B5E3B");
                case "warning": return ("#FFF6E5", "#D9922B", "#7A4D0B");
                case "danger": return ("#FDEDED", "#C93B3B", "#7C1D1D");
                default: return ("#EEF3F9", brand.PrimaryColor, "#1F3552");
            }
        }

        public static string RenderHtml(EmailContent content, EmailBrand? brandOverride = null)
        {
            var brand = brandOverride ?? EmailBrand.Current;
            var sb = new StringBuilder();

            sb.Append("<!DOCTYPE html><html lang=\"en\"><head><meta charset=\"utf-8\"/>");
            sb.Append("<meta name=\"viewport\" content=\"width=device-width, initial-scale=1\"/>");
            sb.Append("<meta name=\"x-apple-disable-message-reformatting\"/>");
            sb.Append("<title>").Append(Enc(content.Subject)).Append("</title>");
            sb.Append("<style>@media only screen and (max-width:620px){.card{width:100%!important}.pad{padding-left:20px!important;padding-right:20px!important}.btn a{display:block!important}}</style>");
            sb.Append("</head>");
            sb.Append("<body style=\"margin:0;padding:0;background-color:#EEF1F5;\">");

            // Hidden preview text shown next to the subject in the inbox.
            sb.Append("<div style=\"display:none;max-height:0;overflow:hidden;opacity:0;color:#EEF1F5;font-size:1px;line-height:1px;\">")
              .Append(Enc(content.Preheader)).Append("</div>");

            sb.Append("<table role=\"presentation\" width=\"100%\" cellspacing=\"0\" cellpadding=\"0\" border=\"0\" style=\"background-color:#EEF1F5;\"><tr><td align=\"center\" style=\"padding:28px 12px;\">");
            sb.Append("<table role=\"presentation\" class=\"card\" width=\"600\" cellspacing=\"0\" cellpadding=\"0\" border=\"0\" style=\"width:600px;max-width:600px;background-color:#FFFFFF;border-radius:10px;overflow:hidden;\">");

            // Header band with logo and application name.
            if (!string.IsNullOrEmpty(brand.LogoUrl))
                sb.Append("<tr><td class=\"pad\" style=\"background-color:#FFFFFF;padding:22px 40px 20px 40px;\"><img src=\"").Append(Enc(brand.LogoUrl)).Append("\" alt=\"").Append(Enc(brand.CompanyName)).Append("\" height=\"40\" style=\"display:block;height:40px;border:0;\"/></td></tr>");
            sb.Append("<tr><td class=\"pad\" style=\"background-color:").Append(brand.SecondaryColor).Append(";padding:18px 40px;\">");
            sb.Append("<span style=\"font-family:").Append(Font).Append(";font-size:22px;font-weight:700;letter-spacing:0.3px;color:").Append(brand.PrimaryColor).Append(";\">").Append(Enc(brand.AppName)).Append("</span>");
            sb.Append("</td></tr>");
            sb.Append("<tr><td style=\"font-size:0;line-height:0;\"><table role=\"presentation\" width=\"100%\" cellspacing=\"0\" cellpadding=\"0\" border=\"0\"><tr>")
              .Append("<td width=\"65%\" style=\"background-color:").Append(brand.PrimaryColor).Append(";height:5px;line-height:5px;font-size:0;\">&nbsp;</td>")
              .Append("<td width=\"35%\" style=\"background-color:").Append(brand.AccentColor).Append(";height:5px;line-height:5px;font-size:0;\">&nbsp;</td>")
              .Append("</tr></table></td></tr>");

            // Body.
            sb.Append("<tr><td class=\"pad\" style=\"padding:36px 40px 12px 40px;font-family:").Append(Font).Append(";color:#2B3441;font-size:15px;line-height:1.65;\">");
            if (!string.IsNullOrWhiteSpace(content.Heading))
                sb.Append("<h1 style=\"margin:0 0 18px 0;font-family:").Append(Font).Append(";font-size:22px;line-height:1.3;font-weight:700;color:").Append(brand.PrimaryColor).Append(";\">").Append(Enc(content.Heading)).Append("</h1>");
            if (!string.IsNullOrWhiteSpace(content.Greeting))
                sb.Append("<p style=\"margin:0 0 16px 0;\">").Append(Rich(content.Greeting)).Append("</p>");

            foreach (var block in content.Blocks)
            {
                switch (block.Kind)
                {
                    case "p":
                        sb.Append("<p style=\"margin:0 0 16px 0;\">").Append(Rich(block.Text)).Append("</p>");
                        break;
                    case "note":
                        sb.Append("<p style=\"margin:0 0 16px 0;font-size:13px;color:#6B7684;\">").Append(Rich(block.Text)).Append("</p>");
                        break;
                    case "bullets":
                        sb.Append("<ul style=\"margin:0 0 16px 0;padding-left:20px;\">");
                        foreach (var item in block.Items)
                            sb.Append("<li style=\"margin:0 0 6px 0;\">").Append(Rich(item)).Append("</li>");
                        sb.Append("</ul>");
                        break;
                    case "details":
                        if (block.Rows.Count == 0) break;
                        sb.Append("<table role=\"presentation\" width=\"100%\" cellspacing=\"0\" cellpadding=\"0\" border=\"0\" style=\"margin:0 0 20px 0;background-color:#F6F8FB;border:1px solid #E1E6EE;border-radius:8px;\">");
                        for (var i = 0; i < block.Rows.Count; i++)
                        {
                            var border = i == block.Rows.Count - 1 ? "" : "border-bottom:1px solid #E1E6EE;";
                            sb.Append("<tr><td style=\"padding:11px 16px;").Append(border).Append("font-family:").Append(Font).Append(";font-size:13px;color:#6B7684;width:38%;vertical-align:top;\">")
                              .Append(Enc(block.Rows[i].Key)).Append("</td><td style=\"padding:11px 16px;").Append(border).Append("font-family:").Append(Font).Append(";font-size:14px;color:#1F2A37;font-weight:600;vertical-align:top;\">")
                              .Append(Rich(block.Rows[i].Value)).Append("</td></tr>");
                        }
                        sb.Append("</table>");
                        break;
                    case "callout":
                        var tone = Tone(brand, block.Tone);
                        sb.Append("<table role=\"presentation\" width=\"100%\" cellspacing=\"0\" cellpadding=\"0\" border=\"0\" style=\"margin:0 0 20px 0;\"><tr><td style=\"background-color:")
                          .Append(tone.Back).Append(";border-left:4px solid ").Append(tone.Edge).Append(";padding:14px 18px;font-family:").Append(Font)
                          .Append(";font-size:14px;line-height:1.6;color:").Append(tone.Ink).Append(";border-radius:4px;\">").Append(Rich(block.Text)).Append("</td></tr></table>");
                        break;
                    case "button":
                        var href = SafeUrl(block.Url);
                        sb.Append("<table role=\"presentation\" cellspacing=\"0\" cellpadding=\"0\" border=\"0\" class=\"btn\" style=\"margin:8px 0 22px 0;\"><tr><td align=\"center\" bgcolor=\"")
                          .Append(brand.PrimaryColor).Append("\" style=\"border-radius:6px;\"><a href=\"").Append(Enc(href))
                          .Append("\" target=\"_blank\" style=\"display:inline-block;padding:13px 28px;font-family:").Append(Font)
                          .Append(";font-size:15px;font-weight:600;color:#FFFFFF;text-decoration:none;border-radius:6px;\">").Append(Enc(block.Text)).Append("</a></td></tr></table>");
                        sb.Append("<p style=\"margin:0 0 18px 0;font-size:12px;line-height:1.5;color:#6B7684;word-break:break-all;\">If the button does not work, copy this link into your browser:<br/><a href=\"")
                          .Append(Enc(href)).Append("\" style=\"color:").Append(brand.PrimaryColor).Append(";\">").Append(Enc(href)).Append("</a></p>");
                        break;
                }
            }

            // Sign-off.
            if (content.Automatic)
                sb.Append("<p style=\"margin:8px 0 24px 0;font-size:13px;color:#6B7684;\">This is an automatic message from ").Append(Enc(brand.AppName)).Append(". Please do not reply to it.</p>");
            else
                sb.Append("<p style=\"margin:8px 0 24px 0;\">Kind regards,<br/><strong>").Append(Enc(brand.TeamName)).Append("</strong><br/><span style=\"font-size:13px;color:#6B7684;\">")
                  .Append(Enc(brand.LegalName)).Append("</span></p>");
            sb.Append("</td></tr>");

            // Footer.
            sb.Append("<tr><td class=\"pad\" style=\"background-color:#F6F8FB;border-top:1px solid #E1E6EE;padding:20px 40px;font-family:").Append(Font)
              .Append(";font-size:12px;line-height:1.6;color:#6B7684;\">");
            sb.Append("<strong style=\"color:#2B3441;\">").Append(Enc(brand.LegalName)).Append("</strong><br/>");
            sb.Append("This message and any link in it are meant only for the person it was sent to. If it reached you by mistake, please tell <a href=\"mailto:")
              .Append(Enc(brand.SupportEmail)).Append("\" style=\"color:").Append(brand.PrimaryColor).Append(";\">").Append(Enc(brand.SupportEmail)).Append("</a> and delete it.");
            sb.Append("</td></tr>");

            sb.Append("</table></td></tr></table></body></html>");
            return sb.ToString();
        }

        public static string RenderText(EmailContent content, EmailBrand? brandOverride = null)
        {
            var brand = brandOverride ?? EmailBrand.Current;
            var sb = new StringBuilder();
            string Plain(string? value) => Bold.Replace(value ?? string.Empty, "$1");

            sb.AppendLine(brand.AppName.ToUpperInvariant());
            sb.AppendLine();
            if (!string.IsNullOrWhiteSpace(content.Heading)) { sb.AppendLine(content.Heading); sb.AppendLine(); }
            if (!string.IsNullOrWhiteSpace(content.Greeting)) { sb.AppendLine(Plain(content.Greeting)); sb.AppendLine(); }
            foreach (var block in content.Blocks)
            {
                switch (block.Kind)
                {
                    case "details":
                        foreach (var row in block.Rows) sb.AppendLine(row.Key + ": " + Plain(row.Value));
                        sb.AppendLine();
                        break;
                    case "bullets":
                        foreach (var item in block.Items) sb.AppendLine("- " + Plain(item));
                        sb.AppendLine();
                        break;
                    case "button":
                        sb.AppendLine(block.Text + ": " + block.Url);
                        sb.AppendLine();
                        break;
                    default:
                        sb.AppendLine(Plain(block.Text));
                        sb.AppendLine();
                        break;
                }
            }
            sb.AppendLine(content.Automatic ? "This is an automatic message from " + brand.AppName + ". Please do not reply to it." : "Kind regards," + Environment.NewLine + brand.TeamName);
            sb.AppendLine();
            sb.AppendLine("-- " + brand.LegalName);
            return sb.ToString();
        }
    }
}