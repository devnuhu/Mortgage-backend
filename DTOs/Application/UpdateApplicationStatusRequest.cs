using System.ComponentModel.DataAnnotations;

namespace MMGT.API.DTOs.Application
{
    public class UpdateApplicationStatusRequest
    {
        [Required]
        [MaxLength(50)]
        public string Status { get; set; } = string.Empty;

        [MaxLength(1000)]
        public string? Comments { get; set; }
    }
}