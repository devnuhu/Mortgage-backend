using System.ComponentModel.DataAnnotations;

namespace MMGT.API.DTOs.Application
{
    public class CreateApplicationRequest
    {
        // Name and phone number are taken from the RSA record in CPAS when the application is created.
        // They are still accepted here so older screens keep working, but the values sent are ignored.
        public string FirstName { get; set; } = string.Empty;

        public string? MiddleName { get; set; }

        public string LastName { get; set; } = string.Empty;

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