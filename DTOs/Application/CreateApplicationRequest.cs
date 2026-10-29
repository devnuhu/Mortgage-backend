using System.ComponentModel.DataAnnotations;
using System.Text;

namespace MMGT.API.DTOs.Application
{
    public class CreateApplicationRequest
    {
        [Required]
        [MaxLength(100)]
        public string FirstName { get; set; } = string.Empty;

        [MaxLength(100)]
        public string? MiddleName { get; set; }

        [Required]
        [MaxLength(100)]
        public string LastName { get; set; } = string.Empty;

        private string _phoneNumber = string.Empty;

        /// <summary>
        /// Borrower phone number - REQUIRED for SMS notifications.
        /// Spaces, dashes, dots and brackets are removed first (browsers often autofill numbers with them).
        /// Accepted formats: +2348012345678, 2348012345678, 08012345678, 8012345678 (10 digits).
        /// SMS output format: 2348012345678 (standardized)
        /// </summary>
        [Required(ErrorMessage = "Phone number is required for SMS notifications")]
        [RegularExpression(@"^(\+?234\d{10}|0\d{10}|\d{10})$",
            ErrorMessage = "Phone number must be a valid Nigerian number with all its digits: 08012345678, 8012345678, 2348012345678 or +2348012345678")]
        [MaxLength(20)]
        public string PhoneNumber
        {
            get => _phoneNumber;
            set => _phoneNumber = CleanPhone(value);
        }

        private static string CleanPhone(string? value)
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

        [Required]
        [MaxLength(15)]
        public string RsaPin { get; set; } = string.Empty;

        [Required]
        [Range(0, double.MaxValue)]
        public decimal EquityValue { get; set; }

        [Required]
        [Range(0, double.MaxValue)]
        public decimal SupplementaryValue { get; set; }

        [Required]
        [Range(0, double.MaxValue)]
        public decimal LoanAmount { get; set; }

        [Required]
        [Range(0, double.MaxValue)]
        public decimal PropertyValue { get; set; }
    }
}