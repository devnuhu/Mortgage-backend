using System.Text;

namespace MMGT.API.Services
{
    /// <summary>
    /// One place for borrower phone numbers.
    /// Clean: keeps digits and a leading "+", so "0708 806-6021" or "+234 (708) 806 6021" become plain numbers.
    /// Normalize: returns 234XXXXXXXXXX for a valid Nigerian number (07066088021, 7066088021, 2347066088021 and +2347066088021 all give 2347066088021).
    ///            A value that is not a valid number is returned cleaned but otherwise unchanged, so nothing is thrown away.
    /// </summary>
    public static class PhoneNumberNormalizer
    {
        public static string Clean(string? value)
        {
            if (string.IsNullOrWhiteSpace(value)) return string.Empty;
            var sb = new StringBuilder();
            foreach (var c in value.Trim())
            {
                if (c >= '0' && c <= '9') sb.Append(c);
                else if (c == '+' && sb.Length == 0) sb.Append(c);
            }
            return sb.ToString();
        }

        public static string Normalize(string? value)
        {
            var cleaned = Clean(value);
            if (cleaned.Length == 0) return string.Empty;

            var digits = cleaned.StartsWith("+") ? cleaned.Substring(1) : cleaned;

            string candidate;
            if (digits.Length == 11 && digits[0] == '0') candidate = "234" + digits.Substring(1);
            else if (digits.Length == 10) candidate = "234" + digits;
            else candidate = digits;

            // 234 + mobile number (first digit 7, 8 or 9) + 9 more digits = 13 digits
            if (candidate.Length == 13 && candidate.StartsWith("234") && "789".IndexOf(candidate[3]) >= 0)
                return candidate;

            return cleaned;
        }
    }
}