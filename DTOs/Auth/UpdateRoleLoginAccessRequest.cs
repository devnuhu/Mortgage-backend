namespace MMGT.API.DTOs.Auth
{
    /// <summary>
    /// Request DTO for updating role login access control settings
    /// </summary>
    public class UpdateRoleLoginAccessRequest
    {
        /// <summary>
        /// Whether login should be enabled for this role
        /// </summary>
        public bool IsLoginEnabled { get; set; }

        /// <summary>
        /// Message to display to users when login is disabled
        /// </summary>
        public string? DisabledMessage { get; set; }

        /// <summary>
        /// Reason for this change (audit trail)
        /// </summary>
        public string? ChangeReason { get; set; }
    }
}
