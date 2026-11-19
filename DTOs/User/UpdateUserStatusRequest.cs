using System.ComponentModel.DataAnnotations;

namespace MMGT.API.DTOs.User
{
    public class UpdateUserStatusRequest
    {
        /// <summary>false = deactivate (user can no longer sign in), true = reactivate.</summary>
        public bool IsActive { get; set; }

        [MaxLength(250)]
        public string? Reason { get; set; }
    }
}