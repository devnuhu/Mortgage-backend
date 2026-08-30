using System.ComponentModel.DataAnnotations;

namespace MMGT.API.Models
{
    /// <summary>
    /// System-wide configuration settings (key-value pairs).
    /// Allows SUPER_ADMIN to configure system behavior without code deployment.
    /// Examples: MaxAssignmentsPerReviewer, WorkloadWarningThreshold, etc.
    /// </summary>
    public class SystemConfiguration
    {
        /// <summary>
        /// Primary key (GUID)
        /// </summary>
        public Guid Id { get; set; } = Guid.NewGuid();

        /// <summary>
        /// Configuration key (unique identifier)
        /// e.g., "MaxAssignmentsPerReviewer", "WorkloadWarningThreshold"
        /// </summary>
        [Required]
        [MaxLength(100)]
        public string ConfigKey { get; set; } = string.Empty;

        /// <summary>
        /// Configuration value (as string, parsed by application as needed)
        /// e.g., "20", "80"
        /// </summary>
        [Required]
        [MaxLength(500)]
        public string ConfigValue { get; set; } = string.Empty;

        /// <summary>
        /// Human-readable description of what this setting does
        /// </summary>
        [MaxLength(500)]
        public string? Description { get; set; }

        /// <summary>
        /// Last modified by (User ID)
        /// </summary>
        [Required]
        public Guid LastModifiedBy { get; set; }

        /// <summary>
        /// Last modified timestamp (UTC)
        /// </summary>
        [Required]
        public DateTime LastModifiedAt { get; set; } = DateTime.UtcNow;
    }
}
