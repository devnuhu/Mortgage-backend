namespace MMGT.API.DTOs.Assignment
{
    /// <summary>
    /// Paginated response for applications with assignments
    /// </summary>
    public class PaginatedApplicationWithAssignmentDto
    {
        public int PageNumber { get; set; }
        public int PageSize { get; set; }
        public int TotalCount { get; set; }
        public int TotalPages { get; set; }
        public bool HasPreviousPage { get; set; }
        public bool HasNextPage { get; set; }
        public List<ApplicationWithAssignmentDto> Applications { get; set; } = new List<ApplicationWithAssignmentDto>();

        /// <summary>
        /// Summary statistics for the current page/filter
        /// </summary>
        public AssignmentSummaryDto Summary { get; set; } = new AssignmentSummaryDto();
    }

    /// <summary>
    /// Summary statistics for all applications with assignments
    /// </summary>
    public class AssignmentSummaryDto
    {
        /// <summary>
        /// Total number of active applications
        /// </summary>
        public int TotalActiveApplications { get; set; }

        /// <summary>
        /// Total active assignments
        /// </summary>
        public int TotalAssignments { get; set; }

        /// <summary>
        /// Number of unique reviewers (ADMIN users) with assignments
        /// </summary>
        public int UniqueReviewerCount { get; set; }

        /// <summary>
        /// Average time applications have been assigned (in hours)
        /// </summary>
        public double AverageAssignedHours { get; set; }

        /// <summary>
        /// Applications assigned for more than 72 hours (3 days)
        /// </summary>
        public int OverdueApplicationsCount { get; set; }

        /// <summary>
        /// Breakdown by reviewer workload
        /// </summary>
        public List<ReviewerWorkloadSummaryDto> ReviewerWorkloads { get; set; } = new List<ReviewerWorkloadSummaryDto>();
    }

    /// <summary>
    /// Summary of workload for each reviewer
    /// </summary>
    public class ReviewerWorkloadSummaryDto
    {
        public Guid ReviewerId { get; set; }
        public string ReviewerName { get; set; } = string.Empty;
        public string ReviewerEmail { get; set; } = string.Empty;
        public int AssignmentCount { get; set; }
        public double AverageAssignedHours { get; set; }
    }
}
