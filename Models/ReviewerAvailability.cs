namespace MMGT.API.Models
{
    public class ReviewerAvailability
    {
        public Guid Id { get; set; } = Guid.NewGuid();

        /// <summary>
        /// Reference to the reviewer user
        /// </summary>
        public Guid ReviewerId { get; set; }
        public User Reviewer { get; set; }

        /// <summary>
        /// Current availability status: Available, Unavailable, OnLeave
        /// </summary>
        public string Status { get; set; } = "Available"; // Available, Unavailable, OnLeave

        /// <summary>
        /// Start date of the current status (e.g., leave start date)
        /// </summary>
        public DateTime StatusStartDate { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// End date of the current status (e.g., leave end date). Null if ongoing.
        /// </summary>
        public DateTime? StatusEndDate { get; set; }

        /// <summary>
        /// Optional reason for unavailability or leave
        /// </summary>
        public string Reason { get; set; }

        /// <summary>
        /// Last updated timestamp
        /// </summary>
        public DateTime LastUpdated { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// User who marked the availability status
        /// </summary>
        public Guid? UpdatedByUserId { get; set; }
    }
}
