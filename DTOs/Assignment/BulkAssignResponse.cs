namespace MMGT.API.DTOs.Assignment
{
    /// <summary>
    /// Response payload for bulk assignment operations.
    /// Contains operation summary (success/failure counts) and detailed messages for client feedback.
    /// </summary>
    public class BulkAssignResponse
    {
        /// <summary>
        /// Number of applications successfully assigned.
        /// </summary>
        public int SuccessCount { get; set; }

        /// <summary>
        /// Number of applications that were not assigned (either already assigned or failed validation).
        /// </summary>
        public int FailureCount { get; set; }

        /// <summary>
        /// List of operation messages and details for the client (e.g., success summary, warnings about skipped applications).
        /// </summary>
        public List<string> Messages { get; set; } = new();

        /// <summary>
        /// IDs of applications that were skipped (e.g., already assigned, invalid, etc.).
        /// Allows client to show which specific applications were not processed.
        /// </summary>
        public List<Guid> SkippedApplicationIds { get; set; } = new();
    }
}
