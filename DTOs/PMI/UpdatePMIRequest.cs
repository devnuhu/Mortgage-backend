using System.ComponentModel.DataAnnotations;

namespace MMGT.API.DTOs.PMI
{
    public class UpdatePMIRequest
    {
        [MaxLength(200)]
        public string? PmiName { get; set; }

        [EmailAddress]
        [MaxLength(256)]
        public string? EmailAddress { get; set; }

        [MaxLength(200)]
        public string? ResourcePersonName { get; set; }

        [EmailAddress]
        [MaxLength(256)]
        public string? ResourcePersonEmail { get; set; }

        [MaxLength(20)]
        public string? PhoneNumber { get; set; }

        [MaxLength(50)]
        public string? Status { get; set; }
    }
}