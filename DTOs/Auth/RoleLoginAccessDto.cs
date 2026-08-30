namespace MMGT.API.DTOs.Auth
{
    /// <summary>
    /// Response DTO for role login access control settings
    /// </summary>
    public class RoleLoginAccessDto
    {
        /// <summary>
        /// Unique identifier for this setting
        /// </summary>
        public Guid Id { get; set; }

        /// <summary>
        /// The role (e.g., "CLIENT", "SERVICE_MANAGER")
        /// </summary>
        public string Role { get; set; } = string.Empty;

        /// <summary>
        /// Whether login is currently enabled for this role
        /// </summary>
        public bool IsLoginEnabled { get; set; }

        /// <summary>
        /// Message shown to users when login is disabled
        /// </summary>
        public string? DisabledMessage { get; set; }

        /// <summary>
        /// Reason for the last change
        /// </summary>
        public string? ChangeReason { get; set; }

        /// <summary>
        /// Whether a schedule is currently active
        /// </summary>
        public bool IsScheduled { get; set; }

        /// <summary>
        /// Start time for scheduled disable (UTC)
        /// </summary>
        public DateTime? ScheduledStartTime { get; set; }

        /// <summary>
        /// End time for scheduled disable (UTC)
        /// </summary>
        public DateTime? ScheduledEndTime { get; set; }

        /// <summary>
        /// Reason for the scheduled change
        /// </summary>
        public string? ScheduledReason { get; set; }

        /// <summary>
        /// User ID who made the last change
        /// </summary>
        public Guid LastModifiedBy { get; set; }

        /// <summary>
        /// When the last change was made (UTC)
        /// </summary>
        public DateTime LastModifiedAt { get; set; }

        /// <summary>
        /// When this setting was created (UTC)
        /// </summary>
        public DateTime CreatedAt { get; set; }
    }
}
