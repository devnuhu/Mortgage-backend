using System.ComponentModel.DataAnnotations;

namespace MMGT.API.DTOs.Auth
{
    /// <summary>
    /// Request DTO for scheduling automatic disable/enable of role login access
    /// Times should be provided in the user's local timezone.
    /// The backend automatically detects and applies the user's timezone from their profile.
    /// No need to manually specify timezone offset - it's handled automatically!
    /// </summary>
    public class ScheduleRoleAccessRequest
    {
        /// <summary>
        /// Start time for scheduled disable in user's local timezone
        /// Must be after current time in user's timezone
        /// Example for Nigeria 3:00 PM: "2026-01-20T15:00:00"
        /// </summary>
        [Required]
        public DateTime ScheduledStartTime { get; set; }

        /// <summary>
        /// End time for scheduled disable in user's local timezone
        /// Must be after ScheduledStartTime
        /// Example for Nigeria 5:00 PM: "2026-01-20T17:00:00"
        /// </summary>
        [Required]
        public DateTime ScheduledEndTime { get; set; }

        /// <summary>
        /// OPTIONAL: Manual timezone offset override (hours from UTC)
        /// If not provided, user's stored timezone preference is used.
        /// Useful for scheduling on behalf of users in different timezones.
        /// Valid range: -14 to +14 hours
        /// Examples: 1 (Nigeria UTC+1), -5 (EST UTC-5), 0 (GMT)
        /// </summary>
        [Range(-14, 14, ErrorMessage = "TimezoneOffsetHours must be between -14 and +14")]
        public int? TimezoneOffsetHours { get; set; } = null; // Auto-detect from user profile

        /// <summary>
        /// Message to display to users during the scheduled disable period
        /// </summary>
        [MaxLength(500)]
        public string? DisabledMessage { get; set; }

        /// <summary>
        /// Reason for scheduling this disable (audit trail)
        /// </summary>
        [MaxLength(1000)]
        public string? ScheduledReason { get; set; }
    }
}


