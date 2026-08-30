using Microsoft.EntityFrameworkCore;
using MMGT.API.Data;
using MMGT.API.Models;
using MMGT.API.Services.Interfaces;

namespace MMGT.API.Services
{
    public class ReviewerAvailabilityService : IReviewerAvailabilityService
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<ReviewerAvailabilityService> _logger;

        public ReviewerAvailabilityService(
            ApplicationDbContext context,
            ILogger<ReviewerAvailabilityService> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<ReviewerAvailability> GetReviewerAvailabilityAsync(Guid reviewerId)
        {
            var availability = await _context.ReviewerAvailabilities
                .AsNoTracking()
                .FirstOrDefaultAsync(a => a.ReviewerId == reviewerId);

            if (availability == null)
            {
                // If no record exists, create default "Available" record
                availability = new ReviewerAvailability
                {
                    ReviewerId = reviewerId,
                    Status = "Available",
                    StatusStartDate = DateTime.UtcNow,
                    LastUpdated = DateTime.UtcNow
                };

                _context.ReviewerAvailabilities.Add(availability);
                await _context.SaveChangesAsync();
            }

            return availability;
        }

        public async Task<List<ReviewerAvailability>> GetUnavailableReviewersAsync()
        {
            _logger.LogInformation("[REVIEWER-AVAILABILITY] GetUnavailableReviewersAsync called");

            // First, get all unavailable records regardless of whether user exists
            var allUnavailableRecords = await _context.ReviewerAvailabilities
                .AsNoTracking()
                .Where(a => a.Status != "Available")
                .ToListAsync();

            _logger.LogInformation($"[REVIEWER-AVAILABILITY] Found {allUnavailableRecords.Count} total unavailable records in database (before user join)");

            // Log details of each unavailable record
            foreach (var rec in allUnavailableRecords)
            {
                _logger.LogInformation($"[REVIEWER-AVAILABILITY] DB Record: ReviewerId={rec.ReviewerId}, Status={rec.Status}, StartDate={rec.StatusStartDate}, EndDate={rec.StatusEndDate}, NOW={DateTime.UtcNow}");
            }

            // Now get all active ADMIN users
            var activeAdminUsers = await _context.Users
                .Where(u => u.IsActive && u.Role == "ADMIN")
                .ToListAsync();

            _logger.LogInformation($"[REVIEWER-AVAILABILITY] Found {activeAdminUsers.Count} active ADMIN users");

            // Filter out expired periods and match with users
            var result = new List<ReviewerAvailability>();
            foreach (var availability in allUnavailableRecords)
            {
                _logger.LogInformation($"[REVIEWER-AVAILABILITY] Processing unavailable record: ReviewerId={availability.ReviewerId}, Status={availability.Status}, EndDate={availability.StatusEndDate}");

                // Check if leave/unavailability period has ended
                if (availability.StatusEndDate.HasValue && availability.StatusEndDate.Value <= DateTime.UtcNow)
                {
                    _logger.LogInformation($"[REVIEWER-AVAILABILITY] Skipping ReviewerId={availability.ReviewerId} - period ended on {availability.StatusEndDate}");
                    continue;
                }

                // Find matching user
                var user = activeAdminUsers.FirstOrDefault(u => u.Id == availability.ReviewerId);

                if (user == null)
                {
                    _logger.LogWarning($"[REVIEWER-AVAILABILITY] ⚠️ ReviewerId={availability.ReviewerId} has Unavailable status but NO matching User record found! User may be deleted or not ADMIN role.");
                    continue;
                }

                _logger.LogInformation($"[REVIEWER-AVAILABILITY] ✅ Including ReviewerId={availability.ReviewerId} ({user.FirstName} {user.LastName})");
                availability.Reviewer = user;
                result.Add(availability);
            }

            _logger.LogInformation($"[REVIEWER-AVAILABILITY] Returning {result.Count} unavailable reviewers");
            return result;
        }

        public async Task<List<ReviewerAvailability>> GetAvailableReviewersAsync()
        {
            // Get all active ADMIN users
            var activeAdminUsers = await _context.Users
                .Where(u => u.IsActive && u.Role == "ADMIN")
                .ToListAsync();

            var availableReviewers = new List<ReviewerAvailability>();

            foreach (var user in activeAdminUsers)
            {
                // Get availability record if it exists
                var availability = await _context.ReviewerAvailabilities
                    .AsNoTracking()
                    .FirstOrDefaultAsync(a => a.ReviewerId == user.Id);

                // If no availability record exists, assume the user is available by default
                if (availability == null)
                {
                    availability = new ReviewerAvailability
                    {
                        ReviewerId = user.Id,
                        Status = "Available",
                        Reviewer = user
                    };
                    availableReviewers.Add(availability);
                }
                // Include users with "Available" status
                else if (availability.Status == "Available")
                {
                    // User is available
                    availability.Reviewer = user;
                    availableReviewers.Add(availability);
                }
                // Also include users whose unavailability/leave period has ended
                else if ((availability.Status == "Unavailable" || availability.Status == "OnLeave") &&
                         availability.StatusEndDate.HasValue && availability.StatusEndDate.Value <= DateTime.UtcNow)
                {
                    // Period has ended, treat as available now
                    availability.Reviewer = user;
                    availableReviewers.Add(availability);
                }
            }

            return availableReviewers;
        }

        public async Task<ReviewerAvailability> MarkAvailableAsync(Guid reviewerId, Guid? updatedByUserId = null)
        {
            var availability = await _context.ReviewerAvailabilities
                .FirstOrDefaultAsync(a => a.ReviewerId == reviewerId);

            if (availability == null)
            {
                availability = new ReviewerAvailability
                {
                    ReviewerId = reviewerId,
                    Status = "Available",
                    StatusStartDate = DateTime.UtcNow,
                    Reason = string.Empty,  // Set to empty string, not null
                    LastUpdated = DateTime.UtcNow,
                    UpdatedByUserId = updatedByUserId
                };

                _context.ReviewerAvailabilities.Add(availability);
            }
            else
            {
                availability.Status = "Available";
                availability.StatusStartDate = DateTime.UtcNow;
                availability.StatusEndDate = null;
                availability.Reason = string.Empty;  // Set to empty string, not null
                availability.LastUpdated = DateTime.UtcNow;
                availability.UpdatedByUserId = updatedByUserId;
            }

            try
            {
                _logger.LogInformation($"[MARK-AVAILABLE] Saving changes for ReviewerId={reviewerId}");
                await _context.SaveChangesAsync();
                _logger.LogInformation($"[MARK-AVAILABLE] SUCCESS - Reviewer {reviewerId} marked as Available");
            }
            catch (Exception ex)
            {
                _logger.LogError($"[MARK-AVAILABLE] ERROR: {ex.Message}");
                _logger.LogError($"[MARK-AVAILABLE] Exception: {ex}");
                throw;
            }

            return availability;
        }

        public async Task<ReviewerAvailability> MarkUnavailableAsync(Guid reviewerId, string reason = null, DateTime? endDate = null, Guid? updatedByUserId = null)
        {
            _logger.LogInformation($"[MARK-UNAVAILABLE] Starting for ReviewerId={reviewerId}, Reason={reason}, EndDate={endDate}");

            var availability = await _context.ReviewerAvailabilities
                .FirstOrDefaultAsync(a => a.ReviewerId == reviewerId);

            _logger.LogInformation($"[MARK-UNAVAILABLE] Found existing record: {(availability != null ? "YES" : "NO")}");

            if (availability == null)
            {
                _logger.LogInformation($"[MARK-UNAVAILABLE] Creating NEW availability record");
                availability = new ReviewerAvailability
                {
                    ReviewerId = reviewerId,
                    Status = "Unavailable",
                    StatusStartDate = DateTime.UtcNow,
                    StatusEndDate = endDate,
                    Reason = reason,
                    LastUpdated = DateTime.UtcNow,
                    UpdatedByUserId = updatedByUserId
                };

                _context.ReviewerAvailabilities.Add(availability);
                _logger.LogInformation($"[MARK-UNAVAILABLE] NEW record added to context");
            }
            else
            {
                _logger.LogInformation($"[MARK-UNAVAILABLE] UPDATING existing record. Current Status={availability.Status}");
                availability.Status = "Unavailable";
                availability.StatusStartDate = DateTime.UtcNow;
                availability.StatusEndDate = endDate;
                availability.Reason = reason;
                availability.LastUpdated = DateTime.UtcNow;
                availability.UpdatedByUserId = updatedByUserId;
                _logger.LogInformation($"[MARK-UNAVAILABLE] Record updated. New Status={availability.Status}");
            }

            try
            {
                _logger.LogInformation($"[MARK-UNAVAILABLE] Calling SaveChangesAsync...");
                var result = await _context.SaveChangesAsync();
                _logger.LogInformation($"[MARK-UNAVAILABLE] SaveChangesAsync completed. Rows affected: {result}");
            }
            catch (Exception ex)
            {
                _logger.LogError($"[MARK-UNAVAILABLE] ERROR during SaveChangesAsync: {ex.Message}");
                _logger.LogError($"[MARK-UNAVAILABLE] Exception: {ex}");
                throw;
            }

            _logger.LogInformation($"[MARK-UNAVAILABLE] SUCCESS - Reviewer {reviewerId} marked as Unavailable: {reason} until {endDate}");

            return availability;
        }

        public async Task<ReviewerAvailability> MarkOnLeaveAsync(Guid reviewerId, DateTime endDate, string reason = null, Guid? updatedByUserId = null)
        {
            var availability = await _context.ReviewerAvailabilities
                .FirstOrDefaultAsync(a => a.ReviewerId == reviewerId);

            if (availability == null)
            {
                availability = new ReviewerAvailability
                {
                    ReviewerId = reviewerId,
                    Status = "OnLeave",
                    StatusStartDate = DateTime.UtcNow,
                    StatusEndDate = endDate,
                    Reason = reason ?? "On Leave",
                    LastUpdated = DateTime.UtcNow,
                    UpdatedByUserId = updatedByUserId
                };

                _context.ReviewerAvailabilities.Add(availability);
            }
            else
            {
                availability.Status = "OnLeave";
                availability.StatusStartDate = DateTime.UtcNow;
                availability.StatusEndDate = endDate;
                availability.Reason = reason ?? "On Leave";
                availability.LastUpdated = DateTime.UtcNow;
                availability.UpdatedByUserId = updatedByUserId;
            }

            await _context.SaveChangesAsync();
            _logger.LogInformation($"Reviewer {reviewerId} marked as OnLeave until {endDate}");

            return availability;
        }

        public async Task<bool> IsReviewerAvailableAsync(Guid reviewerId)
        {
            var availability = await _context.ReviewerAvailabilities
                .AsNoTracking()
                .FirstOrDefaultAsync(a => a.ReviewerId == reviewerId);

            if (availability == null)
            {
                return true; // Default to available if no record exists
            }

            if (availability.Status == "Available")
            {
                return true;
            }

            // If status is OnLeave or Unavailable, check if end date has passed
            if (availability.StatusEndDate.HasValue && availability.StatusEndDate.Value <= DateTime.UtcNow)
            {
                // Auto-mark as available since the leave/unavailability period has ended
                await MarkAvailableAsync(reviewerId);
                return true;
            }

            return false;
        }

        public async Task<List<ReviewerAvailability>> GetAvailabilityHistoryAsync(Guid reviewerId, int days = 30)
        {
            var startDate = DateTime.UtcNow.AddDays(-days);

            return await _context.ReviewerAvailabilities
                .AsNoTracking()
                .Where(a => a.ReviewerId == reviewerId && a.LastUpdated >= startDate)
                .OrderByDescending(a => a.LastUpdated)
                .ToListAsync();
        }

        public async Task<int> GetUnavailableReviewerCountAsync()
        {
            return await _context.ReviewerAvailabilities
                .AsNoTracking()
                .CountAsync(a => a.Status != "Available");
        }

        /// <summary>
        /// Automatically resets expired unavailability/leave records back to Available status.
        /// This is called periodically by the background service to ensure reviewers who return
        /// from leave/unavailability automatically have their status reset without requiring manual intervention.
        /// </summary>
        /// <returns>Number of reviewer availability records updated back to "Available"</returns>
        public async Task<int> AutoResetExpiredUnavailabilityAsync()
        {
            try
            {
                _logger.LogInformation("[AUTO-RESET-AVAILABILITY] Starting automatic availability reset check");

                var now = DateTime.UtcNow;

                // Find all unavailable/on-leave reviewers whose end date has passed
                var expiredUnavailableRecords = await _context.ReviewerAvailabilities
                    .Where(a => a.Status != "Available" && 
                               a.StatusEndDate.HasValue && 
                               a.StatusEndDate.Value <= now)
                    .ToListAsync();

                if (!expiredUnavailableRecords.Any())
                {
                    _logger.LogInformation("[AUTO-RESET-AVAILABILITY] No expired unavailability records found");
                    return 0;
                }

                _logger.LogInformation($"[AUTO-RESET-AVAILABILITY] Found {expiredUnavailableRecords.Count} expired records to reset");

                int updateCount = 0;
                foreach (var record in expiredUnavailableRecords)
                {
                    try
                    {
                        var reviewerId = record.ReviewerId;
                        var previousStatus = record.Status;
                        var endDate = record.StatusEndDate;

                        // Update record back to Available
                        record.Status = "Available";
                        record.StatusStartDate = now;
                        record.StatusEndDate = null;
                        record.Reason = string.Empty;
                        record.LastUpdated = now;
                        // Note: UpdatedByUserId remains null to indicate system auto-update

                        _context.ReviewerAvailabilities.Update(record);
                        updateCount++;

                        _logger.LogInformation(
                            $"[AUTO-RESET-AVAILABILITY] Reset reviewer {reviewerId}: {previousStatus} (ended {endDate}) → Available");
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(
                            $"[AUTO-RESET-AVAILABILITY] Error updating reviewer availability: {ex.Message}");
                    }
                }

                // Batch save all changes
                if (updateCount > 0)
                {
                    await _context.SaveChangesAsync();
                    _logger.LogInformation(
                        $"[AUTO-RESET-AVAILABILITY] Successfully reset {updateCount} reviewer(s) back to Available status");
                }

                return updateCount;
            }
            catch (Exception ex)
            {
                _logger.LogError($"[AUTO-RESET-AVAILABILITY] Critical error in AutoResetExpiredUnavailabilityAsync: {ex.Message}");
                _logger.LogError($"[AUTO-RESET-AVAILABILITY] Exception: {ex}");
                return 0;
            }
        }
    }
}
