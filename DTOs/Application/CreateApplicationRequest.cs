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