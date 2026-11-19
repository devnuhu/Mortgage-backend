using System.Net;
using System.Text;
using System.Text.RegularExpressions;

namespace MMGT.API.Services.Email
{
    /// <summary>One piece of an email: a paragraph, a details table, a bullet list, a call-out, a section label, an item box, a button or a small note.</summary>
    public sealed class EmailBlock
    {
        public string Kind { get; private init; } = "p";
        public string Text { get; private init; } = string.Empty;
        public string Title { get; private init; } = string.Empty;
        public string Url { get; private init; } = string.Empty;
        public string Tone { get; private init; } = "info";
        public List<KeyValuePair<string, string>> Rows { get; private init; } = new List<KeyValuePair<string, string>>();
        public List<string> Items { get; private init; } = new List<string>();

        /// <summary>A paragraph. Put **words** between double stars to make them bold.</summary>
        public static EmailBlock Paragraph(string text) => new EmailBlock { Kind = "p", Text = text };

        /// <summary>A small grey note.</summary>
        public static EmailBlock Note(string text) => new EmailBlock { Kind = "note", Text = text };

        /// <summary>A small capital-letter label that introduces the block below it.</summary>
        public static EmailBlock Section(string text) => new EmailBlock { Kind = "section", Text = text };

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

        /// <summary>One entry of a list (for example one rejected document): a bold title, the text under it and a coloured edge.</summary>
        public static EmailBlock Item(string title, string text, string tone = "info") => new EmailBlock { Kind = "item", Title = title, Text = text, Tone = tone };

        public static EmailBlock Button(string text, string url) => new EmailBlock { Kind = "button", Text = text, Url = url };
    }

    /// <summary>What an email says. The layout turns it into HTML and plain text.</summary>
    public sealed class EmailContent
    {
        public string Subject { get; init; } = string.Empty;
        public string Heading { get; init; } = string.Empty;
        public string Preheader { get; init; } = string.Empty;
        public string Greeting { get; init; } = string.Empty;

        /// <summary>Optional small status pill above the heading (for example "Rejected").</summary>
        public string Badge { get; init; } = string.Empty;

        /// <summary>Colour of the pill: info, success, warning or danger.</summary>
        public string BadgeTone { get; init; } = "info";

        /// <summary>Optional grey line under the heading (for example the application number).</summary>
        public string Reference { get; init; } = string.Empty;

        public List<EmailBlock> Blocks { get; init; } = new List<EmailBlock>();

        /// <summary>When true the sign-off reads as an automatic message, not a personal one.</summary>
        public bool Automatic { get; init; }
    }

    /// <summary>
    /// Turns EmailContent into a corporate, responsive email: accent bar, branded header, white card,
    /// clear typography, bulletproof button and a dark footer with the support contact.
    /// Tables and inline styles only, so it works in Outlook, Gmail and on phones.
    /// Every value is HTML-encoded here, so names and comments can never inject markup.
    /// </summary>
    public static class EmailLayout
    {
        private static readonly Regex Bold = new Regex(@"\*\*(.+?)\*\*", RegexOptions.Compiled);
        private const string Font = "'Segoe UI', Helvetica, Arial, sans-serif";
        private const string Ink = "#0F2747";
        private const string Muted = "#6B7684";
        private const string Line = "#E3E8EF";

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

        /// <summary>Turns the part of an email address before the @ into a name: "aisha.olabisi@x.com" becomes "Aisha Olabisi".</summary>
        public static string NameFromEmail(string? email)
        {
            var local = (email ?? string.Empty).Split('@')[0];
            var parts = local.Split(new[] { '.', '_', '-' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0) return "there";
            return string.Join(" ", parts.Select(p => char.ToUpperInvariant(p[0]) + p.Substring(1).ToLowerInvariant()));
        }

        private static (string Back, string Edge, string Ink) Tone(EmailBrand brand, string tone)
        {
            switch (tone)
            {
                case "success": return ("#EFF9F3", "#2E9D63", "#1B5E3B");
                case "warning": return ("#FFF8EB", "#D9922B", "#7A4D0B");
                case "danger": return ("#FDF3F3", "#C93B3B", "#5C1A1A");
                default: return ("#EEF3FA", brand.PrimaryColor, "#1F3552");
            }
        }

        private static (string Back, string Ink) PillTone(string tone)
        {
            switch (tone)
            {
                case "success": return ("#E3F5EA", "#1B5E3B");
                case "warning": return ("#FFF1D6", "#7A4D0B");
                case "danger": return ("#FDEDED", "#9B1C1C");
                default: return ("#E4EDF8", "#003883");
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
            sb.Append("<style>@media only screen and (max-width:640px){.card{width:100%!important}.pad{padding-left:22px!important;padding-right:22px!important}.btn a{display:block!important}}</style>");
            sb.Append("</head>");
            sb.Append("<body style=\"margin:0;padding:0;background-color:#F1F4F8;\">");

            // Hidden preview text shown next to the subject in the inbox.
            sb.Append("<div style=\"display:none;max-height:0;overflow:hidden;opacity:0;color:#F1F4F8;font-size:1px;line-height:1px;\">")
              .Append(Enc(content.Preheader)).Append("</div>");

            sb.Append("<table role=\"presentation\" width=\"100%\" cellspacing=\"0\" cellpadding=\"0\" border=\"0\" style=\"background-color:#F1F4F8;\"><tr><td align=\"center\" style=\"padding:32px 12px;\">");
            sb.Append("<table role=\"presentation\" class=\"card\" width=\"640\" cellspacing=\"0\" cellpadding=\"0\" border=\"0\" style=\"width:640px;max-width:640px;background-color:#FFFFFF;border-radius:12px;overflow:hidden;border:1px solid " + Line + ";\">");

            // Accent bar.
            sb.Append("<tr><td style=\"font-size:0;line-height:0;\"><table role=\"presentation\" width=\"100%\" cellspacing=\"0\" cellpadding=\"0\" border=\"0\"><tr>")
              .Append("<td width=\"70%\" style=\"background-color:").Append(brand.PrimaryColor).Append(";height:6px;line-height:6px;font-size:0;\">&nbsp;</td>")
              .Append("<td width=\"30%\" style=\"background-color:").Append(brand.AccentColor).Append(";height:6px;line-height:6px;font-size:0;\">&nbsp;</td>")
              .Append("</tr></table></td></tr>");

            // Header: company name on the left, application name on the right.
            sb.Append("<tr><td class=\"pad\" style=\"padding:24px 44px 20px 44px;border-bottom:1px solid #E9EDF3;\"><table role=\"presentation\" width=\"100%\" cellspacing=\"0\" cellpadding=\"0\" border=\"0\"><tr>");
            sb.Append("<td align=\"left\" style=\"font-family:").Append(Font).Append(";\">");
            sb.Append("<span style=\"font-size:21px;font-weight:700;letter-spacing:0.2px;color:").Append(brand.PrimaryColor).Append(";\">").Append(Enc(brand.CompanyName)).Append("</span>");
            sb.Append("</td>");
            sb.Append("<td align=\"right\" style=\"font-family:").Append(Font).Append(";font-size:11px;color:" + Muted + ";letter-spacing:0.7px;text-transform:uppercase;\">").Append(Enc(brand.AppName)).Append("</td>");
            sb.Append("</tr></table></td></tr>");

            // Body.
            sb.Append("<tr><td class=\"pad\" style=\"padding:34px 44px 8px 44px;font-family:").Append(Font).Append(";color:#2B3441;font-size:15px;line-height:1.65;\">");
            if (!string.IsNullOrWhiteSpace(content.Badge))
            {
                var pill = PillTone(content.BadgeTone);
                sb.Append("<span style=\"display:inline-block;padding:5px 12px;border-radius:999px;background-color:").Append(pill.Back).Append(";color:").Append(pill.Ink)
                  .Append(";font-size:12px;font-weight:700;letter-spacing:0.8px;text-transform:uppercase;margin:0 0 14px 0;\">").Append(Enc(content.Badge)).Append("</span>");
            }
            if (!string.IsNullOrWhiteSpace(content.Heading))
                sb.Append("<h1 style=\"margin:0 0 6px 0;font-family:").Append(Font).Append(";font-size:26px;line-height:1.25;font-weight:700;color:" + Ink + ";\">").Append(Enc(content.Heading)).Append("</h1>");
            if (!string.IsNullOrWhiteSpace(content.Reference))
                sb.Append("<p style=\"margin:0 0 22px 0;font-size:14px;color:" + Muted + ";\">").Append(Enc(content.Reference)).Append("</p>");
            else
                sb.Append("<div style=\"height:12px;line-height:12px;font-size:0;\">&nbsp;</div>");
            if (!string.IsNullOrWhiteSpace(content.Greeting))
                sb.Append("<p style=\"margin:0 0 14px 0;\">").Append(Rich(content.Greeting)).Append("</p>");

            foreach (var block in content.Blocks)
            {
                switch (block.Kind)
                {
                    case "p":
                        sb.Append("<p style=\"margin:0 0 18px 0;\">").Append(Rich(block.Text)).Append("</p>");
                        break;
                    case "note":
                        sb.Append("<p style=\"margin:0 0 18px 0;font-size:13px;color:" + Muted + ";\">").Append(Rich(block.Text)).Append("</p>");
                        break;
                    case "section":
                        sb.Append("<p style=\"margin:0 0 10px 0;font-size:12px;font-weight:700;color:" + Muted + ";text-transform:uppercase;letter-spacing:0.8px;\">").Append(Enc(block.Text)).Append("</p>");
                        break;
                    case "bullets":
                        sb.Append("<ul style=\"margin:0 0 18px 0;padding-left:20px;\">");
                        foreach (var item in block.Items)
                            sb.Append("<li style=\"margin:0 0 6px 0;\">").Append(Rich(item)).Append("</li>");
                        sb.Append("</ul>");
                        break;
                    case "details":
                        if (block.Rows.Count == 0) break;
                        sb.Append("<table role=\"presentation\" width=\"100%\" cellspacing=\"0\" cellpadding=\"0\" border=\"0\" style=\"margin:0 0 24px 0;border:1px solid " + Line + ";border-radius:8px;\">");
                        for (var i = 0; i < block.Rows.Count; i++)
                        {
                            var border = i == block.Rows.Count - 1 ? "" : "border-bottom:1px solid #EDF0F5;";
                            sb.Append("<tr><td style=\"padding:12px 18px;").Append(border).Append("font-family:").Append(Font).Append(";font-size:12px;color:" + Muted + ";width:36%;vertical-align:top;text-transform:uppercase;letter-spacing:0.5px;\">")
                              .Append(Enc(block.Rows[i].Key)).Append("</td><td style=\"padding:12px 18px;").Append(border).Append("font-family:").Append(Font).Append(";font-size:14px;color:" + Ink + ";font-weight:600;vertical-align:top;\">")
                              .Append(Rich(block.Rows[i].Value)).Append("</td></tr>");
                        }
                        sb.Append("</table>");
                        break;
                    case "callout":
                        var tone = Tone(brand, block.Tone);
                        sb.Append("<table role=\"presentation\" width=\"100%\" cellspacing=\"0\" cellpadding=\"0\" border=\"0\" style=\"margin:0 0 24px 0;\"><tr><td style=\"background-color:")
                          .Append(tone.Back).Append(";border-left:4px solid ").Append(tone.Edge).Append(";padding:14px 18px;font-family:").Append(Font)
                          .Append(";font-size:14px;line-height:1.6;color:").Append(tone.Ink).Append(";border-radius:4px;\">").Append(Rich(block.Text)).Append("</td></tr></table>");
                        break;
                    case "item":
                        var edge = Tone(brand, block.Tone).Edge;
                        sb.Append("<table role=\"presentation\" width=\"100%\" cellspacing=\"0\" cellpadding=\"0\" border=\"0\" style=\"margin:0 0 10px 0;\"><tr><td style=\"background-color:#FFFFFF;border:1px solid " + Line + ";border-left:4px solid ")
                          .Append(edge).Append(";padding:13px 18px;font-family:").Append(Font).Append(";font-size:14px;line-height:1.55;color:#2B3441;border-radius:4px;\"><strong style=\"color:" + Ink + ";\">")
                          .Append(Enc(block.Title)).Append("</strong><br/>").Append(Rich(block.Text)).Append("</td></tr></table>");
                        break;
                    case "button":
                        var href = SafeUrl(block.Url);
                        sb.Append("<table role=\"presentation\" cellspacing=\"0\" cellpadding=\"0\" border=\"0\" class=\"btn\" style=\"margin:14px 0 20px 0;\"><tr><td align=\"center\" bgcolor=\"")
                          .Append(brand.PrimaryColor).Append("\" style=\"border-radius:8px;\"><a href=\"").Append(Enc(href))
                          .Append("\" target=\"_blank\" style=\"display:inline-block;padding:14px 30px;font-family:").Append(Font)
                          .Append(";font-size:15px;font-weight:600;color:#FFFFFF;text-decoration:none;border-radius:8px;\">").Append(Enc(block.Text)).Append("</a></td></tr></table>");
                        sb.Append("<p style=\"margin:0 0 18px 0;font-size:12px;line-height:1.5;color:" + Muted + ";word-break:break-all;\">If the button does not work, copy this link into your browser:<br/><a href=\"")
                          .Append(Enc(href)).Append("\" style=\"color:").Append(brand.PrimaryColor).Append(";\">").Append(Enc(href)).Append("</a></p>");
                        break;
                }
            }

            // Sign-off.
            if (content.Automatic)
                sb.Append("<p style=\"margin:8px 0 28px 0;font-size:13px;color:" + Muted + ";\">This is an automatic message from ").Append(Enc(brand.AppName)).Append(". Please do not reply to it.</p>");
            else
                sb.Append("<p style=\"margin:8px 0 28px 0;\">Kind regards,<br/><strong>").Append(Enc(brand.TeamName)).Append("</strong><br/><span style=\"font-size:13px;color:" + Muted + ";\">")
                  .Append(Enc(brand.LegalName)).Append("</span></p>");
            sb.Append("</td></tr>");

            // Footer.
            sb.Append("<tr><td class=\"pad\" style=\"background-color:#0F2747;padding:26px 44px;font-family:").Append(Font)
              .Append(";font-size:12px;line-height:1.7;color:#B9C5D8;\">");
            sb.Append("<strong style=\"color:#FFFFFF;font-size:13px;\">").Append(Enc(brand.LegalName)).Append("</strong><br/>");
            sb.Append("Need help? Contact <a href=\"mailto:").Append(Enc(brand.SupportEmail)).Append("\" style=\"color:#FFB866;text-decoration:none;font-weight:600;\">").Append(Enc(brand.SupportEmail)).Append("</a><br/>");
            sb.Append("<span style=\"color:#8FA0BA;\">This message and any link in it are meant only for the person it was sent to. If it reached you by mistake, please tell ")
              .Append(Enc(brand.SupportEmail)).Append(" and delete it.</span>");
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
            if (!string.IsNullOrWhiteSpace(content.Heading)) { sb.AppendLine(content.Heading); }
            if (!string.IsNullOrWhiteSpace(content.Reference)) { sb.AppendLine(content.Reference); }
            sb.AppendLine();
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
                    case "section":
                        sb.AppendLine(block.Text.ToUpperInvariant());
                        break;
                    case "item":
                        sb.AppendLine("* " + block.Title + " - " + Plain(block.Text));
                        break;
                    default:
                        sb.AppendLine(Plain(block.Text));
                        sb.AppendLine();
                        break;
                }
            }
            sb.AppendLine();
            sb.AppendLine(content.Automatic ? "This is an automatic message from " + brand.AppName + ". Please do not reply to it." : "Kind regards," + Environment.NewLine + brand.TeamName);
            sb.AppendLine();
            sb.AppendLine("If you have any issue, please contact " + brand.SupportEmail + ".");
            sb.AppendLine("-- " + brand.LegalName);
            return sb.ToString();
        }
    }
}
