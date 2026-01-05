using System.ComponentModel.DataAnnotations;

namespace MMGT.API.DTOs.PMI
{
    public class CreatePMIRequest
    {
        [Required]
        [MaxLength(200)]
        public string PmiName { get; set; } = string.Empty;

        [Required]
        [EmailAddress]
        [MaxLength(256)]
        public string EmailAddress { get; set; } = string.Empty;

        [Required]
        [MaxLength(200)]
        public string ResourcePersonName { get; set; } = string.Empty;

        [Required]
        [EmailAddress]
        [MaxLength(256)]
        public string ResourcePersonEmail { get; set; } = string.Empty;

        [Required]
        [MaxLength(20)]
        public string PhoneNumber { get; set; } = string.Empty;
    }
}