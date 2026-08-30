namespace MMGT.API.DTOs.Auth
{
    /// <summary>
    /// Response DTO for login access disabled error (403 Forbidden)
    /// </summary>
    public class LoginAccessDisabledResponse
    {
        /// <summary>
        /// Machine-readable error code: "LOGIN_ACCESS_DISABLED"
        /// </summary>
        public string Code { get; set; } = "LOGIN_ACCESS_DISABLED";

        /// <summary>
        /// Human-readable message for the user
        /// </summary>
        public string Message { get; set; } = string.Empty;

        /// <summary>
        /// Timestamp when this error occurred (UTC)
        /// </summary>
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    }
}
