using MMGT.API.Models;

namespace MMGT.API.Services.Interfaces
{
    public interface IReviewerAvailabilityService
    {
        /// <summary>
        /// Get availability status for a specific reviewer
        /// </summary>
        Task<ReviewerAvailability> GetReviewerAvailabilityAsync(Guid reviewerId);

        /// <summary>
        /// Get all reviewers currently unavailable or on leave
        /// </summary>
        Task<List<ReviewerAvailability>> GetUnavailableReviewersAsync();

        /// <summary>
        /// Get all reviewers currently available
        /// </summary>
        Task<List<ReviewerAvailability>> GetAvailableReviewersAsync();

        /// <summary>
        /// Mark a reviewer as available
        /// </summary>
        Task<ReviewerAvailability> MarkAvailableAsync(Guid reviewerId, Guid? updatedByUserId = null);

        /// <summary>
        /// Mark a reviewer as unavailable with optional reason and end date
        /// </summary>
        Task<ReviewerAvailability> MarkUnavailableAsync(Guid reviewerId, string reason = null, DateTime? endDate = null, Guid? updatedByUserId = null);

        /// <summary>
        /// Mark a reviewer as on leave with end date
        /// </summary>
        Task<ReviewerAvailability> MarkOnLeaveAsync(Guid reviewerId, DateTime endDate, string reason = null, Guid? updatedByUserId = null);

        /// <summary>
        /// Check if a reviewer is currently available for new assignments
        /// </summary>
        Task<bool> IsReviewerAvailableAsync(Guid reviewerId);

        /// <summary>
        /// Get availability history for a reviewer (recent status changes)
        /// </summary>
        Task<List<ReviewerAvailability>> GetAvailabilityHistoryAsync(Guid reviewerId, int days = 30);

        /// <summary>
        /// Get count of unavailable reviewers
        /// </summary>
        Task<int> GetUnavailableReviewerCountAsync();

        /// <summary>
        /// Auto-reset reviewer availability for expired unavailability/leave records.
        /// Called periodically by background service to restore reviewers back to Available when their unavailability window expires.
        /// </summary>
        /// <returns>Number of reviewers whose status was reset back to Available</returns>
        Task<int> AutoResetExpiredUnavailabilityAsync();
    }
}
