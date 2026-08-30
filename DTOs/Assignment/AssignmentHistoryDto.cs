namespace MMGT.API.DTOs.Assignment
{
    /// <summary>
    /// DTO representing a single assignment in the assignment history timeline.
    /// Shows who was assigned, when, and why (for reassignments).
    /// </summary>
    public class AssignmentHistoryDto
    {
        /// <summary>
        /// Unique identifier for this assignment record
        /// </summary>
        public Guid Id { get; set; }

        /// <summary>
        /// Application ID being assigned
        /// </summary>
        public Guid ApplicationId { get; set; }

        /// <summary>
        /// Current/previous reviewer assigned to this application
        /// </summary>
        public Guid ReviewerId { get; set; }

        /// <summary>
        /// Name of the reviewer
        /// </summary>
        public string ReviewerName { get; set; } = string.Empty;

        /// <summary>
        /// Email of the reviewer
        /// </summary>
        public string ReviewerEmail { get; set; } = string.Empty;

        /// <summary>
        /// Name of the previous reviewer (if reassignment)
        /// </summary>
        public string? PreviousReviewerName { get; set; }

        /// <summary>
        /// Email of the previous reviewer (if reassignment)
        /// </summary>
        public string? PreviousReviewerEmail { get; set; }

        /// <summary>
        /// Who made the assignment (null = System auto-assignment)
        /// </summary>
        public Guid? AssignedBy { get; set; }

        /// <summary>
        /// Name of who made the assignment (or "System" for auto-assignment)
        /// </summary>
        public string AssignedByName { get; set; } = "System";

        /// <summary>
        /// When the assignment was made (UTC)
        /// </summary>
        public DateTime AssignedAt { get; set; }

        /// <summary>
        /// Reason for reassignment (null for initial assignment)
        /// Examples: "Reviewer on leave", "Workload balancing", "Skill-based"
        /// </summary>
        public string? ReassignmentReason { get; set; }

        /// <summary>
        /// Is this the current active assignment?
        /// </summary>
        public bool IsActive { get; set; }

        /// <summary>
        /// Type of assignment event for UI display
        /// Values: "Initial", "Reassigned"
        /// </summary>
        public string AssignmentType => string.IsNullOrEmpty(ReassignmentReason) ? "Initial" : "Reassigned";
    }
}
