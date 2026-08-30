using System.ComponentModel.DataAnnotations;

namespace MMGT.API.DTOs.Auth
{
    public class AdminResetPasswordRequest
    {
        [Required]
        public Guid UserId { get; set; }
    }
}
