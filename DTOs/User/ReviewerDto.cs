namespace MMGT.API.DTOs.User
{
    /// <summary>
    /// Simplified reviewer/user DTO for selection lists and assignment workflows
    /// Used by Lead Reviewers when selecting reviewers for manual assignment/reassignment
    /// </summary>
    public class ReviewerDto
    {
        public Guid Id { get; set; }
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Role { get; set; } = string.Empty;
        public bool IsActive { get; set; }

        /// <summary>
        /// Full name for easy display in UI (e.g., "John Doe")
        /// </summary>
        public string FullName => $"{FirstName} {LastName}".Trim();
    }

    /// <summary>
    /// Extended reviewer DTO with availability and workload information
    /// Used when Lead Reviewers need to make informed assignment decisions
    /// </summary>
    public class ReviewerWithWorkloadDto : ReviewerDto
    {
        /// <summary>
        /// Current number of active assignments
        /// </summary>
        public int ActiveAssignmentCount { get; set; }

        /// <summary>
        /// Is the reviewer currently available for new assignments
        /// </summary>
        public bool IsAvailable { get; set; }

        /// <summary>
        /// Reviewer's current approval rate (percentage)
        /// </summary>
        public double ApprovalRatePercentage { get; set; }

        /// <summary>
        /// Performance rating (if available)
        /// </summary>
        public string? PerformanceRating { get; set; }

        /// <summary>
        /// If reviewer is unavailable, the reason why
        /// </summary>
        public string? UnavailabilityReason { get; set; }

        /// <summary>
        /// When the reviewer's unavailability ends (if applicable)
        /// </summary>
        public DateTime? UnavailableUntil { get; set; }
    }
}
