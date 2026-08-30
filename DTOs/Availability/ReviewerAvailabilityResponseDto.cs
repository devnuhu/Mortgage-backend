namespace MMGT.API.DTOs.Availability
{
    /// <summary>
    /// Clean response DTO for reviewer availability - contains only necessary fields for frontend
    /// Excludes sensitive data like password hashes and internal user information
    /// </summary>
    public class ReviewerAvailabilityResponseDto
    {
        /// <summary>
        /// Unique identifier for the availability record (internal use only)
        /// DO NOT use this for API endpoints - use ReviewerId instead
        /// </summary>
        public Guid Id { get; set; }

        /// <summary>
        /// Reference to the reviewer user ID - USE THIS for all availability API endpoints
        /// (e.g., PUT /api/analytics/reviewers/{reviewerId}/availability/unavailable)
        /// </summary>
        public Guid ReviewerId { get; set; }

        /// <summary>
        /// Reviewer's first name
        /// </summary>
        public string FirstName { get; set; } = string.Empty;

        /// <summary>
        /// Reviewer's last name
        /// </summary>
        public string LastName { get; set; } = string.Empty;

        /// <summary>
        /// Reviewer's email address
        /// </summary>
        public string Email { get; set; } = string.Empty;

        /// <summary>
        /// Reviewer's role (e.g., ADMIN, REVIEWER)
        /// </summary>
        public string Role { get; set; } = string.Empty;

        /// <summary>
        /// Current availability status: Available, Unavailable, OnLeave
        /// </summary>
        public string Status { get; set; } = "Available";

        /// <summary>
        /// Reason for unavailability or leave (if applicable)
        /// </summary>
        public string? Reason { get; set; }

        /// <summary>
        /// When the current status started
        /// </summary>
        public DateTime StatusStartDate { get; set; }

        /// <summary>
        /// When the current status ends (null if ongoing)
        /// </summary>
        public DateTime? StatusEndDate { get; set; }

        /// <summary>
        /// Number of active assignments currently assigned to this reviewer
        /// Used for workload distribution display
        /// </summary>
        public int ActiveAssignmentCount { get; set; }

        /// <summary>
        /// Full name for easy display in UI (e.g., "John Doe")
        /// </summary>
        public string FullName => $"{FirstName} {LastName}".Trim();
    }
}
