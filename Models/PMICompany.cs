using System.ComponentModel.DataAnnotations;

namespace MMGT.API.Models
{
    public class PMICompany
    {
        public Guid Id { get; set; }

        [Required]
        [MaxLength(200)]
        public string PmiName { get; set; } = string.Empty;

        [Required]
        [MaxLength(256)]
        public string EmailAddress { get; set; } = string.Empty;

        [Required]
        [MaxLength(200)]
        public string ResourcePersonName { get; set; } = string.Empty;

        [Required]
        [MaxLength(256)]
        public string ResourcePersonEmail { get; set; } = string.Empty;

        [Required]
        [MaxLength(20)]
        public string PhoneNumber { get; set; } = string.Empty;

        [Required]
        [MaxLength(50)]
        public string Status { get; set; } = "ACTIVE"; // ACTIVE, INACTIVE, SUSPENDED

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        // Navigation properties
        public virtual ICollection<User> Users { get; set; } = new List<User>();
        public virtual ICollection<Application> Applications { get; set; } = new List<Application>();
    }
}
