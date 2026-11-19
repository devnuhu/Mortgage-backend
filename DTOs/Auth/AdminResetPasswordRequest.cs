using System.ComponentModel.DataAnnotations;

namespace MMGT.API.DTOs.Auth
{
    public class AdminResetPasswordRequest
    {
        [Required]
        public Guid UserId { get; set; }
        /// <summary>True (default): email the temporary password to the user. False: do not email; return it in the response for the admin to hand over.</summary>
        public bool SendEmail { get; set; } = true;
    }
}
