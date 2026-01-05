using System.ComponentModel.DataAnnotations;

namespace MMGT.API.DTOs.Application
{
    public class UpdateApplicationRequest
    {
        [MaxLength(100)]
        public string? FirstName { get; set; }

        [MaxLength(100)]
        public string? MiddleName { get; set; }

        [MaxLength(100)]
        public string? LastName { get; set; }

        [MaxLength(15)]
        public string? RsaPin { get; set; }

        [Range(0, double.MaxValue)]
        public decimal? EquityValue { get; set; }

        [Range(0, double.MaxValue)]
        public decimal? SupplementaryValue { get; set; }

        [Range(0, double.MaxValue)]
        public decimal? LoanAmount { get; set; }

        [Range(0, double.MaxValue)]
        public decimal? PropertyValue { get; set; }

        [MaxLength(50)]
        public string? Priority { get; set; }
    }
}
