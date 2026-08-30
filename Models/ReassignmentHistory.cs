namespace MMGT.API.Models
{
    public class ReassignmentHistory
    {
        public Guid Id { get; set; } = Guid.NewGuid();

        /// <summary>
        /// Reference to the application assignment that was reassigned
        /// </summary>
        public Guid ApplicationAssignmentId { get; set; }
        public ApplicationAssignment ApplicationAssignment { get; set; }

        /// <summary>
        /// The application ID being reassigned
        /// </summary>
        public Guid ApplicationId { get; set; }

        /// <summary>
        /// Previous reviewer ID before reassignment
        /// </summary>
        public Guid PreviousReviewerId { get; set; }
        public User PreviousReviewer { get; set; }

        /// <summary>
        /// New reviewer ID after reassignment
        /// </summary>
        public Guid NewReviewerId { get; set; }
        public User NewReviewer { get; set; }

        /// <summary>
        /// Reason for reassignment: Manual, AutoBalance, AvailabilityChange, PerformanceIssue, CriteriaBased
        /// </summary>
        public string ReassignmentReason { get; set; } // Manual, AutoBalance, AvailabilityChange, PerformanceIssue, CriteriaBased

        /// <summary>
        /// Detailed message explaining the reassignment
        /// </summary>
        public string ReassignmentDetails { get; set; }

        /// <summary>
        /// User who initiated the reassignment (null if system-initiated)
        /// </summary>
        public Guid? ReassignedByUserId { get; set; }
        public User ReassignedByUser { get; set; }

        /// <summary>
        /// Timestamp of the reassignment
        /// </summary>
        public DateTime ReassignedAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Previous assignment status at time of reassignment
        /// </summary>
        public string PreviousAssignmentStatus { get; set; }

        /// <summary>
        /// New assignment status after reassignment
        /// </summary>
        public string NewAssignmentStatus { get; set; }

        /// <summary>
        /// Whether the reassignment was approved/completed
        /// </summary>
        public bool IsCompleted { get; set; } = true;
    }
}
