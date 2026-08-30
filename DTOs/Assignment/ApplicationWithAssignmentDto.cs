namespace MMGT.API.DTOs.Assignment
{
    /// <summary>
    /// DTO for displaying applications with their current assignment details
    /// Used for bulk reassignment operations by Lead Reviewers
    /// </summary>
    public class ApplicationWithAssignmentDto
    {
        /// <summary>
        /// Application ID
        /// </summary>
        public Guid ApplicationId { get; set; }

        /// <summary>
        /// Application reference number
        /// </summary>
        public string ApplicationNumber { get; set; } = string.Empty;

        /// <summary>
        /// Application status (e.g., Pending, Under Review, Completed)
        /// </summary>
        public string ApplicationStatus { get; set; } = string.Empty;

        /// <summary>
        /// Application priority (e.g., High, Medium, Low)
        /// </summary>
        public string ApplicationPriority { get; set; } = string.Empty;

        /// <summary>
        /// Borrower's first name
        /// </summary>
        public string BorrowerFirstName { get; set; } = string.Empty;

        /// <summary>
        /// Borrower's last name
        /// </summary>
        public string BorrowerLastName { get; set; } = string.Empty;

        /// <summary>
        /// RSA PIN of the borrower
        /// </summary>
        public string RsaPin { get; set; } = string.Empty;

        /// <summary>
        /// Loan amount requested
        /// </summary>
        public decimal LoanAmount { get; set; }

        /// <summary>
        /// Current assignment ID
        /// </summary>
        public Guid AssignmentId { get; set; }

        /// <summary>
        /// Current reviewer (ADMIN user) ID
        /// </summary>
        public Guid ReviewerId { get; set; }

        /// <summary>
        /// Current reviewer (ADMIN user) full name
        /// </summary>
        public string ReviewerName { get; set; } = string.Empty;

        /// <summary>
        /// Current reviewer (ADMIN user) email
        /// </summary>
        public string ReviewerEmail { get; set; } = string.Empty;

        /// <summary>
        /// When was this assignment made
        /// </summary>
        public DateTime AssignedAt { get; set; }

        /// <summary>
        /// How long (in hours) the application has been assigned to this reviewer
        /// </summary>
        public double AssignedForHours { get; set; }

        /// <summary>
        /// Whether this assignment is currently active
        /// </summary>
        public bool IsActive { get; set; }

        /// <summary>
        /// Reason for the last reassignment (if any)
        /// </summary>
        public string? ReassignmentReason { get; set; }

        /// <summary>
        /// Who made the current assignment
        /// </summary>
        public Guid AssignedBy { get; set; }

        /// <summary>
        /// When the application was created
        /// </summary>
        public DateTime ApplicationCreatedAt { get; set; }

        /// <summary>
        /// When the application was submitted for review
        /// </summary>
        public DateTime? ApplicationSubmittedAt { get; set; }
    }
}
