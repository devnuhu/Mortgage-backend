
using System;
using System.ComponentModel.DataAnnotations;

namespace MMGT.API.DTOs.User
{
    public class CreateUserRequest
    {
        [Required]
        [EmailAddress]
        public string Email { get; set; } = string.Empty;

        [Required]
        [MaxLength(100)]
        public string FirstName { get; set; } = string.Empty;

        [Required]
        [MaxLength(100)]
        public string LastName { get; set; } = string.Empty;

        [Required]
        public string Role { get; set; } = string.Empty;

        public Guid? PMICompanyId { get; set; }

        
        [Required]
        [MinLength(8)]
        public string TemporaryPassword { get; set; } = string.Empty;
    }
}
