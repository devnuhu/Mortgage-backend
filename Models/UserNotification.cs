using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MMGT.API.Models
{
    /// <summary>In-app message shown in the bell for one user.</summary>
    [Table("Notifications")]
    public class UserNotification
    {
        public Guid Id { get; set; }
        public Guid UserId { get; set; }
        [Required, MaxLength(50)] public string Type { get; set; } = string.Empty;
        [Required, MaxLength(200)] public string Title { get; set; } = string.Empty;
        [Required, MaxLength(1000)] public string Message { get; set; } = string.Empty;
        public Guid? ApplicationId { get; set; }
        [MaxLength(300)] public string? LinkPath { get; set; }
        public bool IsRead { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? ReadAt { get; set; }
    }
}