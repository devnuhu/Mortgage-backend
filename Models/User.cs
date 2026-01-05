using System.ComponentModel.DataAnnotations;

namespace MMGT.API.Models
{
    public class User
    {
        public Guid Id { get; set; }

        [Required]
        [MaxLength(256)]
        public string Email { get; set; } = string.Empty;

        [Required]
        [MaxLength(100)]
        public string FirstName { get; set; } = string.Empty;

        [Required]
        [MaxLength(100)]
        public string LastName { get; set; } = string.Empty;

        [Required]
        [MaxLength(50)]
        public string Role { get; set; } = string.Empty; // SUPER_ADMIN, ADMIN, CLIENT

        [Required]
        public string PasswordHash { get; set; } = string.Empty;

        public bool IsFirstLogin { get; set; } = true;
        public bool IsActive { get; set; } = true;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? LastLoginAt { get; set; }

        // Navigation properties
        public Guid? PMICompanyId { get; set; }

        // 🆕 ADD THIS PROPERTY
        [MaxLength(256)]
        public string? PMIEmail { get; set; }

        public virtual PMICompany? PMICompany { get; set; }
        
        [MaxLength(128)]
        public string? FirstLoginTokenHash { get; set; }

        public DateTime? FirstLoginTokenExpiresAt { get; set; }
    }
}