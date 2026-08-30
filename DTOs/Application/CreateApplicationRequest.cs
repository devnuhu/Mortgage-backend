using System.ComponentModel.DataAnnotations;

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

        /// <summary>
        /// Borrower phone number - REQUIRED for SMS notifications
        /// Must be a valid Nigerian phone number
        /// Accepted formats: +2348012345678, 08012345678, 2348012345678
        /// </summary>
        [Required(ErrorMessage = "Phone number is required for SMS notifications")]
        [RegularExpression(@"^\+?234\d{10}$|^0\d{10}$", 
            ErrorMessage = "Phone number must be a valid Nigerian number (10-13 digits with country code 234 or leading 0)")]
        [MaxLength(20)]
        public string PhoneNumber { get; set; } = string.Empty;

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
