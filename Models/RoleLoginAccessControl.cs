using System.ComponentModel.DataAnnotations;

namespace MMGT.API.Models
{
    /// <summary>
    /// Manages login access control for specific roles (CLIENT, SERVICE_MANAGER).
    /// Allows LEAD_REVIEWER/ADMIN to enable/disable login for entire roles.
    /// Supports time-based scheduling for automatic enable/disable.
    /// Includes audit trail for compliance and tracking.
    /// </summary>
    public class RoleLoginAccessControl
    {
        /// <summary>
        /// Primary key (GUID)
        /// </summary>
        public Guid Id { get; set; } = Guid.NewGuid();

        /// <summary>
        /// The role this setting applies to (e.g., "CLIENT", "SERVICE_MANAGER")
        /// </summary>
        [Required]
        [MaxLength(50)]
        public string Role { get; set; } = string.Empty;

        /// <summary>
        /// Whether login is currently enabled for this role
        /// </summary>
        public bool IsLoginEnabled { get; set; } = true;

        /// <summary>
        /// Display message shown to users when login is disabled
        /// </summary>
        [MaxLength(500)]
        public string? DisabledMessage { get; set; }

        /// <summary>
        /// Reason for disabling/enabling login (audit trail)
        /// </summary>
        [MaxLength(1000)]
        public string? ChangeReason { get; set; }

        /// <summary>
        /// Whether a schedule is currently active for this role
        /// </summary>
        public bool IsScheduled { get; set; } = false;

        /// <summary>
        /// Start time for scheduled disable (UTC). Access will be disabled starting at this time.
        /// </summary>
        public DateTime? ScheduledStartTime { get; set; }

        /// <summary>
        /// End time for scheduled disable (UTC). Access will be re-enabled after this time.
        /// </summary>
        public DateTime? ScheduledEndTime { get; set; }

        /// <summary>
        /// Reason for the scheduled change (audit trail)
        /// </summary>
        [MaxLength(1000)]
        public string? ScheduledReason { get; set; }

        /// <summary>
        /// User ID who last changed this setting (audit trail)
        /// </summary>
        [Required]
        public Guid LastModifiedBy { get; set; }

        /// <summary>
        /// Timestamp of last change (UTC)
        /// </summary>
        [Required]
        public DateTime LastModifiedAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// When this setting was created (UTC)
        /// </summary>
        [Required]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
