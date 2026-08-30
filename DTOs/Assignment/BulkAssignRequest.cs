namespace MMGT.API.DTOs.Assignment
{
    /// <summary>
    /// Request payload for bulk assigning multiple unassigned applications to a single reviewer.
    /// </summary>
    public class BulkAssignRequest
    {
        /// <summary>
        /// IDs of applications to assign. Must contain at least one application.
        /// </summary>
        public List<Guid> ApplicationIds { get; set; } = new();

        /// <summary>
        /// ID of the target reviewer. Must be a valid, active reviewer with ADMIN or LEAD_REVIEWER role.
        /// </summary>
        public Guid ReviewerId { get; set; }

        /// <summary>
        /// Optional reason or notes for the bulk assignment (stored in ApplicationAssignment.ReassignmentReason for audit trail).
        /// </summary>
        public string? Reason { get; set; }
    }
}
