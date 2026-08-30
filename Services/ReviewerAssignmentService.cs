using Microsoft.EntityFrameworkCore;
using MMGT.API.Data;
using MMGT.API.DTOs.Assignment;
using MMGT.API.Models;

namespace MMGT.API.Services
{
    /// <summary>
    /// Service for managing reviewer assignments.
    /// Handles auto-assignment when applications are submitted and manual reassignment by Lead Reviewers.
    /// </summary>
    public interface IReviewerAssignmentService
    {
        Task<ApplicationAssignment> AutoAssignApplicationAsync(Guid applicationId);
        Task<ApplicationAssignment> AssignApplicationAsync(Guid applicationId, Guid reviewerId, Guid assignedBy, string? reason = null);
        Task<ApplicationAssignment> ManuallyReassignApplicationAsync(Guid applicationId, Guid newReviewerId, Guid reassignedBy, string reason);
        Task<ApplicationAssignment?> GetCurrentAssignmentAsync(Guid applicationId);
        Task<List<ApplicationAssignment>> GetAssignmentHistoryAsync(Guid applicationId);
        Task<List<Application>> GetReviewerAssignmentsAsync(Guid reviewerId);
        Task<int> GetReviewerWorkloadAsync(Guid reviewerId);
        Task<PaginatedApplicationWithAssignmentDto> GetUnassignedApplicationsForBulkAssignmentAsync(
            int pageNumber = 1,
            int pageSize = 50,
            string? sortBy = null,
            bool sortDescending = true,
            string? applicationStatus = null,
            Guid? pmiCompanyId = null,
            string? rsaPin = null,
            string? borrowerName = null,
            DateTime? createdFromDate = null,
            DateTime? createdToDate = null);
        Task<DTOs.Assignment.BulkAssignResponse> BulkAssignApplicationsAsync(
            List<Guid> applicationIds,
            Guid targetReviewerId,
            Guid performedBy,
            string? reason = null);
    }

    public class ReviewerAssignmentService : IReviewerAssignmentService
    {
        private readonly ApplicationDbContext _context;
        private readonly ISystemConfigurationService _configService;
        private readonly IReviewerPerformanceService _performanceService;
        private readonly ILogger<ReviewerAssignmentService> _logger;

        public ReviewerAssignmentService(
            ApplicationDbContext context, 
            ISystemConfigurationService configService,
            IReviewerPerformanceService performanceService,
            ILogger<ReviewerAssignmentService> logger)
        {
            _context = context;
            _configService = configService;
            _performanceService = performanceService;
            _logger = logger;
        }

        /// <summary>
        /// Auto-assign an application to the reviewer with the lowest current workload.
        /// Uses round-robin with workload balancing.
        /// Deactivates any previous assignments and creates a new active one (IsActive=true).
        /// Called when application is submitted.
        /// Assigns to both ADMIN and LEAD_REVIEWER roles to ensure availability.
        /// </summary>
        public async Task<ApplicationAssignment> AutoAssignApplicationAsync(Guid applicationId)
        {
            _logger.LogInformation($"[AUTO-ASSIGN-SERVICE] >>> ENTERING AutoAssignApplicationAsync for applicationId: {applicationId}");

            var application = await _context.Applications.FindAsync(applicationId);
            _logger.LogInformation($"[AUTO-ASSIGN-SERVICE] Application lookup: Found={application != null}, AppNumber={application?.ApplicationNumber}, Status={application?.Status}");

            if (application == null)
            {
                _logger.LogError($"[AUTO-ASSIGN-SERVICE] ✗ Application with ID '{applicationId}' not found in database");
                throw new InvalidOperationException($"Application with ID '{applicationId}' not found");
            }

            // Get max assignments per reviewer from system config (default: 20)
            _logger.LogInformation($"[AUTO-ASSIGN-SERVICE] Attempting to retrieve MaxAssignmentsPerReviewer from config...");
            var maxAssignmentsStr = await _configService.GetConfigValueAsync("MaxAssignmentsPerReviewer", "20");
            _logger.LogInformation($"[AUTO-ASSIGN-SERVICE] Config retrieved: maxAssignmentsStr='{maxAssignmentsStr}'");

            int.TryParse(maxAssignmentsStr, out int maxAssignments);
            if (maxAssignments <= 0) maxAssignments = 20;

            _logger.LogInformation($"[AUTO-ASSIGN-SERVICE] Max assignments per reviewer: {maxAssignments}");

            try
            {
                _logger.LogInformation($"[AUTO-ASSIGN-SERVICE] Starting auto-assignment process for application {applicationId}");

                // Deactivate any previous assignments
                _logger.LogInformation($"[AUTO-ASSIGN-SERVICE] Checking for previous assignments...");
                var previousAssignments = await _context.ApplicationAssignments
                    .Where(a => a.ApplicationId == applicationId && a.IsActive)
                    .ToListAsync();

                _logger.LogInformation($"[AUTO-ASSIGN-SERVICE] Found {previousAssignments.Count} previous active assignments");

                foreach (var previous in previousAssignments)
                {
                    previous.IsActive = false;
                    _context.ApplicationAssignments.Update(previous);
                    _logger.LogDebug($"[AUTO-ASSIGN-SERVICE] Deactivated assignment: {previous.Id}");
                }

                _logger.LogInformation($"[AUTO-ASSIGN-SERVICE] Saving deactivated assignments...");
                await _context.SaveChangesAsync();
                _logger.LogInformation($"[AUTO-ASSIGN-SERVICE] ✓ Deactivated assignments saved");

                // Find reviewer with lowest workload - ADMIN role (reviewers) who are AVAILABLE
                _logger.LogInformation($"[AUTO-ASSIGN-SERVICE] Querying for active ADMIN reviewers with Available status...");

                // Get all active ADMIN users
                var allAdminUsers = await _context.Users
                    .Where(u => u.IsActive && u.Role == "ADMIN")
                    .ToListAsync();
                _logger.LogInformation($"[AUTO-ASSIGN-SERVICE] Total active ADMIN users: {allAdminUsers.Count}");
                foreach (var user in allAdminUsers)
                {
                    _logger.LogInformation($"[AUTO-ASSIGN-SERVICE]   - {user.FirstName} {user.LastName} ({user.Id})");
                }

                // Get all availability records
                var allAvailabilities = await _context.ReviewerAvailabilities.ToListAsync();
                _logger.LogInformation($"[AUTO-ASSIGN-SERVICE] Total availability records in DB: {allAvailabilities.Count}");
                foreach (var avail in allAvailabilities)
                {
                    _logger.LogInformation($"[AUTO-ASSIGN-SERVICE]   - ReviewerId: {avail.ReviewerId}, Status: {avail.Status}");
                }

                var reviewers = await _context.Users
                    .Where(u => u.IsActive && u.Role == "ADMIN")
                    .GroupJoin(_context.ReviewerAvailabilities,
                        u => u.Id,
                        a => a.ReviewerId,
                        (u, availabilities) => new { User = u, Availabilities = availabilities })
                    .Where(x => !x.Availabilities.Any() || 
                                x.Availabilities.Any(a => a.Status == "Available" || 
                                                          ((a.Status == "Unavailable" || a.Status == "OnLeave") && a.StatusEndDate.HasValue && a.StatusEndDate < DateTime.UtcNow)))
                    .Select(x => x.User)
                    .ToListAsync();

                _logger.LogInformation($"[AUTO-ASSIGN-SERVICE] Current UTC Time: {DateTime.UtcNow}");

                // Log detailed availability analysis
                var allUsersWithAvailability = await _context.Users
                    .Where(u => u.IsActive && u.Role == "ADMIN")
                    .GroupJoin(_context.ReviewerAvailabilities,
                        u => u.Id,
                        a => a.ReviewerId,
                        (u, availabilities) => new { User = u, Availabilities = availabilities })
                    .ToListAsync();

                foreach (var item in allUsersWithAvailability)
                {
                    if (item.Availabilities.Any())
                    {
                        var avail = item.Availabilities.FirstOrDefault();
                        _logger.LogInformation($"[AUTO-ASSIGN-SERVICE] {item.User.FirstName} {item.User.LastName}: Status={avail.Status}, EndDate={avail.StatusEndDate}, IsExpired={(avail.StatusEndDate.HasValue && avail.StatusEndDate < DateTime.UtcNow)}, Included={(reviewers.Any(r => r.Id == item.User.Id))}");
                    }
                    else
                    {
                        _logger.LogInformation($"[AUTO-ASSIGN-SERVICE] {item.User.FirstName} {item.User.LastName}: No availability record, Included={(reviewers.Any(r => r.Id == item.User.Id))}");
                    }
                }
                if (!reviewers.Any())
                {
                    _logger.LogError($"[AUTO-ASSIGN-SERVICE] ✗ No available ADMIN reviewers found! All reviewers may be Unavailable or OnLeave.");
                    throw new InvalidOperationException("No available ADMIN reviewers found. All reviewers are currently Unavailable or OnLeave. Please mark at least one reviewer as Available.");
                }

                _logger.LogInformation($"[AUTO-ASSIGN-SERVICE] Calculating workload for {reviewers.Count} reviewers...");

                // Calculate current workload for each reviewer
                var reviewerWorkloads = new Dictionary<Guid, int>();
                foreach (var reviewer in reviewers)
                {
                    var workload = await _context.ApplicationAssignments
                        .Where(a => a.ReviewerId == reviewer.Id && a.IsActive)
                        .CountAsync();
                    reviewerWorkloads[reviewer.Id] = workload;
                    _logger.LogInformation($"[AUTO-ASSIGN-SERVICE] Reviewer {reviewer.FirstName} {reviewer.LastName} ({reviewer.Email}): {workload} active assignments");
                }

                // Find reviewer with lowest workload (that is not over max)
                _logger.LogInformation($"[AUTO-ASSIGN-SERVICE] Selecting reviewer with lowest workload (max: {maxAssignments})...");
                var selectedReviewerId = reviewerWorkloads
                    .Where(kvp => kvp.Value < maxAssignments)
                    .OrderBy(kvp => kvp.Value)
                    .FirstOrDefault()
                    .Key;

                // If all reviewers are at or over max, select the one with lowest load anyway
                if (selectedReviewerId == Guid.Empty)
                {
                    _logger.LogWarning($"[AUTO-ASSIGN-SERVICE] All reviewers at/over max ({maxAssignments}). Selecting lowest workload anyway.");
                    selectedReviewerId = reviewerWorkloads
                        .OrderBy(kvp => kvp.Value)
                        .First()
                        .Key;
                }

                var selectedReviewer = reviewers.First(r => r.Id == selectedReviewerId);
                _logger.LogInformation($"[AUTO-ASSIGN-SERVICE] ✓ Selected reviewer: {selectedReviewer.FirstName} {selectedReviewer.LastName} ({selectedReviewer.Email}) with {reviewerWorkloads[selectedReviewerId]} assignments");

                // Create new assignment (AssignedBy=null indicates system auto-assignment)
                _logger.LogInformation($"[AUTO-ASSIGN-SERVICE] Creating new assignment record...");
                var assignment = new ApplicationAssignment
                {
                    Id = Guid.NewGuid(),
                    ApplicationId = applicationId,
                    ReviewerId = selectedReviewerId,
                    AssignedBy = null, // System auto-assignment
                    AssignedAt = DateTime.UtcNow,
                    ReassignmentReason = null,
                    IsActive = true
                };

                _context.ApplicationAssignments.Add(assignment);
                _logger.LogInformation($"[AUTO-ASSIGN-SERVICE] Assignment record added: {assignment.Id}");

                _logger.LogInformation($"[AUTO-ASSIGN-SERVICE] Saving assignment...");
                await _context.SaveChangesAsync();
                _logger.LogInformation($"[AUTO-ASSIGN-SERVICE] ✓ Assignment saved");

                // Recalculate metrics for this reviewer to reflect the new assignment
                try
                {
                    _logger.LogInformation($"[AUTO-ASSIGN-SERVICE] Recalculating metrics for reviewer {selectedReviewerId}...");
                    await _performanceService.RecalculateMetricsAsync(selectedReviewerId);
                    _logger.LogInformation($"[AUTO-ASSIGN-SERVICE] ✓ Metrics recalculated");
                }
                catch (Exception ex)
                {
                    _logger.LogError($"[AUTO-ASSIGN-SERVICE] ⚠ Failed to recalculate metrics: {ex.Message}");
                    // Don't fail the auto-assignment if metrics calculation fails
                }

                _logger.LogInformation($"[AUTO-ASSIGN-SERVICE] <<< EXITING AutoAssignApplicationAsync - SUCCESS");
                return assignment;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"[AUTO-ASSIGN-SERVICE] ✗ EXCEPTION during auto-assignment. Type: {ex.GetType().Name}, Message: {ex.Message}");
                _logger.LogInformation($"[AUTO-ASSIGN-SERVICE] <<< EXITING AutoAssignApplicationAsync - FAILED");
                throw;
            }
        }

        /// <summary>
         /// Assign an application to a reviewer for the first time.
         /// Used when application has no existing assignment (e.g., legacy applications).
         /// Only LEAD_REVIEWER users can assign applications.
         /// Reason is optional but recommended for audit trail.
         /// </summary>
        public async Task<ApplicationAssignment> AssignApplicationAsync(
            Guid applicationId,
            Guid reviewerId,
            Guid assignedBy,
            string? reason = null)
        {
            var application = await _context.Applications.FindAsync(applicationId);
            if (application == null)
            {
                throw new InvalidOperationException($"Application with ID '{applicationId}' not found");
            }

            var reviewer = await _context.Users.FindAsync(reviewerId);
            if (reviewer == null || !reviewer.IsActive)
            {
                throw new InvalidOperationException($"Reviewer with ID '{reviewerId}' not found or inactive");
            }

            if (reviewer.Role != "ADMIN")
            {
                throw new InvalidOperationException($"User with ID '{reviewerId}' is not an ADMIN reviewer");
            }

            // Check if an active assignment already exists
            var existingActiveAssignment = await _context.ApplicationAssignments
                .FirstOrDefaultAsync(a => a.ApplicationId == applicationId && a.IsActive);

            if (existingActiveAssignment != null)
            {
                throw new InvalidOperationException($"Application {applicationId} already has an active assignment. Use reassign endpoint instead.");
            }

            // Create new assignment
            var newAssignment = new ApplicationAssignment
            {
                Id = Guid.NewGuid(),
                ApplicationId = applicationId,
                ReviewerId = reviewerId,
                AssignedBy = assignedBy,
                AssignedAt = DateTime.UtcNow,
                ReassignmentReason = reason,
                IsActive = true
            };

            _context.ApplicationAssignments.Add(newAssignment);
            await _context.SaveChangesAsync();

            // Recalculate metrics for this reviewer to reflect the new assignment
            try
            {
                await _performanceService.RecalculateMetricsAsync(reviewerId);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Failed to recalculate metrics for reviewer {reviewerId} after assignment: {ex.Message}");
                // Don't fail the assignment if metrics calculation fails
            }

            return newAssignment;
        }

        /// <summary>
         /// Manually reassign an application to a different reviewer.
         /// Only LEAD_REVIEWER users can reassign applications.
         /// Creates new assignment and deactivates previous one.
         /// Reason is mandatory for audit trail.
         /// </summary>
        public async Task<ApplicationAssignment> ManuallyReassignApplicationAsync(
            Guid applicationId, 
            Guid newReviewerId, 
            Guid reassignedBy, 
            string reason)
        {
            var application = await _context.Applications.FindAsync(applicationId);
            if (application == null)
            {
                throw new InvalidOperationException($"Application with ID '{applicationId}' not found");
            }

            var newReviewer = await _context.Users.FindAsync(newReviewerId);
            if (newReviewer == null || !newReviewer.IsActive)
            {
                throw new InvalidOperationException($"Reviewer with ID '{newReviewerId}' not found or inactive");
            }

            if (newReviewer.Role != "ADMIN")
            {
                throw new InvalidOperationException($"User with ID '{newReviewerId}' is not an ADMIN reviewer");
            }

            using (var transaction = await _context.Database.BeginTransactionAsync())
            {
                try
                {
                    // Deactivate current assignment
                    var currentAssignment = await _context.ApplicationAssignments
                        .FirstOrDefaultAsync(a => a.ApplicationId == applicationId && a.IsActive);

                    var previousReviewerId = currentAssignment?.ReviewerId;

                    if (currentAssignment != null)
                    {
                        currentAssignment.IsActive = false;
                        _context.ApplicationAssignments.Update(currentAssignment);
                        await _context.SaveChangesAsync();
                    }

                    // Create new assignment
                    var newAssignment = new ApplicationAssignment
                    {
                        Id = Guid.NewGuid(),
                        ApplicationId = applicationId,
                        ReviewerId = newReviewerId,
                        AssignedBy = reassignedBy, // Track who performed the reassignment
                        AssignedAt = DateTime.UtcNow,
                        ReassignmentReason = reason,
                        IsActive = true
                    };

                    _context.ApplicationAssignments.Add(newAssignment);
                    await _context.SaveChangesAsync();

                    // Create reassignment history entry for audit trail
                    if (previousReviewerId.HasValue && previousReviewerId != newReviewerId)
                    {
                        var reassignmentHistory = new ReassignmentHistory
                        {
                            ApplicationAssignmentId = newAssignment.Id,
                            ApplicationId = applicationId,
                            PreviousReviewerId = previousReviewerId.Value,
                            NewReviewerId = newReviewerId,
                            ReassignmentReason = reason,
                            ReassignmentDetails = reason,
                            ReassignedAt = DateTime.UtcNow,
                            ReassignedByUserId = reassignedBy,
                            PreviousAssignmentStatus = "Active",
                            NewAssignmentStatus = "Active",
                            IsCompleted = true
                        };

                        _context.ReassignmentHistories.Add(reassignmentHistory);
                        await _context.SaveChangesAsync();
                    }

                    await transaction.CommitAsync();

                    // Recalculate metrics for both reviewers (old and new)
                    try
                    {
                        if (previousReviewerId.HasValue)
                        {
                            await _performanceService.RecalculateMetricsAsync(previousReviewerId.Value);
                        }
                        await _performanceService.RecalculateMetricsAsync(newReviewerId);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError($"Failed to recalculate metrics after reassignment: {ex.Message}");
                        // Don't fail the reassignment if metrics calculation fails
                    }

                    return newAssignment;
                }
                catch
                {
                    await transaction.RollbackAsync();
                    throw;
                }
            }
        }

        /// <summary>
        /// Get the current active assignment for an application.
        /// </summary>
        public async Task<ApplicationAssignment?> GetCurrentAssignmentAsync(Guid applicationId)
        {
            return await _context.ApplicationAssignments
                .Include(a => a.Reviewer)
                .FirstOrDefaultAsync(a => a.ApplicationId == applicationId && a.IsActive);
        }

        /// <summary>
        /// Get all assignment history for an application (including inactive assignments).
        /// Sorted by AssignedAt descending (newest first).
        /// </summary>
        public async Task<List<ApplicationAssignment>> GetAssignmentHistoryAsync(Guid applicationId)
        {
            return await _context.ApplicationAssignments
                .Where(a => a.ApplicationId == applicationId)
                .Include(a => a.Reviewer)
                .Include(a => a.AssignedByUser)
                .OrderByDescending(a => a.AssignedAt)
                .ToListAsync();
        }

        /// <summary>
        /// Get unassigned applications with optional filtering, sorting, and pagination.
        /// Mirrors the bulk reassignment query pattern for consistency.
        /// Returns applications with NO active assignment (IQueryable query pattern).
        /// </summary>
        public async Task<PaginatedApplicationWithAssignmentDto>
            GetUnassignedApplicationsForBulkAssignmentAsync(
                int pageNumber = 1,
                int pageSize = 30,
                string? sortBy = null,
                bool sortDescending = true,
                string? applicationStatus = null,
                Guid? pmiCompanyId = null,
                string? rsaPin = null,
                string? borrowerName = null,
                DateTime? createdFromDate = null,
                DateTime? createdToDate = null)
        {
            try
            {
                // Validate pagination
                pageNumber = Math.Max(1, pageNumber);
                pageSize = Math.Max(1, Math.Min(pageSize, 100)); // Cap at 100

                // Base query: Applications with NO active assignment (anti-join pattern)
                var query = _context.Applications.AsNoTracking();

                // CRITICAL: Exclude draft applications - only show submitted/under review applications
                query = query.Where(a => a.Status != "draft");

                // Optional filters (dynamic composition)
                if (!string.IsNullOrEmpty(applicationStatus))
                {
                    query = query.Where(a => a.Status == applicationStatus);
                }

                if (pmiCompanyId.HasValue && pmiCompanyId != Guid.Empty)
                {
                    query = query.Where(a => a.PmiCompanyId == pmiCompanyId);
                }

                if (!string.IsNullOrWhiteSpace(rsaPin))
                {
                    var rsaPinPattern = $"%{rsaPin.Trim()}%";
                    query = query.Where(a => EF.Functions.Like(a.RsaPin, rsaPinPattern));
                }

                if (!string.IsNullOrWhiteSpace(borrowerName))
                {
                    var namePattern = $"%{borrowerName.Trim()}%";
                    query = query.Where(a =>
                        EF.Functions.Like(a.FirstName, namePattern) ||
                        EF.Functions.Like(a.LastName, namePattern) ||
                        EF.Functions.Like(a.MiddleName, namePattern));
                }

                if (createdFromDate.HasValue)
                {
                    query = query.Where(a => a.CreatedAt >= createdFromDate);
                }

                if (createdToDate.HasValue)
                {
                    // Include entire end-of-day
                    var toDateEndOfDay = createdToDate.Value.AddDays(1);
                    query = query.Where(a => a.CreatedAt < toDateEndOfDay);
                }

                // Filter: unassigned applications (no active assignments)
                query = query.Where(a => !a.Assignments.Any(aa => aa.IsActive));

                // Apply sorting
                query = sortBy?.ToLower() switch
                {
                    "applicationnumber" => sortDescending
                        ? query.OrderByDescending(a => a.ApplicationNumber)
                        : query.OrderBy(a => a.ApplicationNumber),

                    "applicantname" => sortDescending
                        ? query.OrderByDescending(a => a.FirstName).ThenByDescending(a => a.LastName)
                        : query.OrderBy(a => a.FirstName).ThenBy(a => a.LastName),

                    "status" => sortDescending
                        ? query.OrderByDescending(a => a.Status)
                        : query.OrderBy(a => a.Status),

                    "createdat" => sortDescending
                        ? query.OrderByDescending(a => a.CreatedAt)
                        : query.OrderBy(a => a.CreatedAt),

                    "submittedat" => sortDescending
                        ? query.OrderByDescending(a => a.SubmittedAt ?? DateTime.MinValue)
                        : query.OrderBy(a => a.SubmittedAt ?? DateTime.MinValue),

                    _ => query.OrderByDescending(a => a.CreatedAt) // Default: newest first
                };

                // Get total count before pagination
                var totalCount = await query.CountAsync();
                var totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);

                // Apply pagination
                var applications = await query
                    .Skip((pageNumber - 1) * pageSize)
                    .Take(pageSize)
                    .ToListAsync();

                // Project to DTO
                var applicationDtos = applications.Select(a => new ApplicationWithAssignmentDto
                {
                    ApplicationId = a.Id,
                    ApplicationNumber = a.ApplicationNumber,
                    ApplicationStatus = a.Status,
                    ApplicationPriority = a.Priority ?? "Normal",
                    BorrowerFirstName = a.FirstName ?? string.Empty,
                    BorrowerLastName = a.LastName ?? string.Empty,
                    RsaPin = a.RsaPin ?? string.Empty,
                    LoanAmount = a.LoanAmount,
                    AssignmentId = Guid.Empty,
                    ReviewerId = Guid.Empty,
                    ReviewerName = "Unassigned",
                    ReviewerEmail = string.Empty,
                    AssignedAt = DateTime.MinValue,
                    AssignedForHours = 0,
                    IsActive = false,
                    ReassignmentReason = null,
                    AssignedBy = Guid.Empty,
                    ApplicationCreatedAt = a.CreatedAt,
                    ApplicationSubmittedAt = a.SubmittedAt
                }).ToList();

                return new PaginatedApplicationWithAssignmentDto
                {
                    PageNumber = pageNumber,
                    PageSize = pageSize,
                    TotalCount = totalCount,
                    TotalPages = totalPages,
                    HasPreviousPage = pageNumber > 1,
                    HasNextPage = pageNumber < totalPages,
                    Applications = applicationDtos,
                    Summary = new AssignmentSummaryDto
                    {
                        TotalActiveApplications = totalCount,
                        TotalAssignments = totalCount,
                        UniqueReviewerCount = 0  // All unassigned
                    }
                };
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error getting unassigned applications for bulk assignment: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Bulk assign multiple unassigned applications to a single reviewer.
        /// Creates assignment records, maintains audit trail via ReassignmentReason field, and recalculates metrics.
        /// Returns operation summary with success/failure counts and details.
        /// </summary>
        public async Task<BulkAssignResponse> BulkAssignApplicationsAsync(
            List<Guid> applicationIds,
            Guid targetReviewerId,
            Guid performedBy,
            string? reason = null)
        {
            var response = new BulkAssignResponse();

            try
            {
                // 1. Validate inputs
                if (applicationIds == null || !applicationIds.Any())
                {
                    response.Messages.Add("No applications provided for assignment");
                    return response;
                }

                if (targetReviewerId == Guid.Empty)
                {
                    response.Messages.Add("Invalid target reviewer");
                    return response;
                }

                // 2. Validate target reviewer exists and has correct role
                var targetReviewer = await _context.Users
                    .FirstOrDefaultAsync(u =>
                        u.Id == targetReviewerId &&
                        u.IsActive &&
                        (u.Role == "ADMIN" || u.Role == "LEAD_REVIEWER" || u.Role == "REVIEWER"));

                if (targetReviewer == null)
                {
                    response.FailureCount = applicationIds.Count;
                    response.Messages.Add($"Target reviewer {targetReviewerId} not found or inactive");
                    return response;
                }

                // 3. Materialize IDs to HashSet (avoid EF Core OPENJSON issues)
                var idSet = new HashSet<Guid>(applicationIds);

                // 4. Load applications
                var applications = await _context.Applications
                    .Where(a => idSet.Contains(a.Id))
                    .ToListAsync();

                if (!applications.Any())
                {
                    response.FailureCount = applicationIds.Count;
                    response.Messages.Add("No matching applications found");
                    return response;
                }

                // 5. Check for already-assigned applications
                var alreadyAssignedIds = await _context.ApplicationAssignments
                    .Where(aa => aa.IsActive && idSet.Contains(aa.ApplicationId))
                    .Select(aa => aa.ApplicationId)
                    .ToListAsync();

                if (alreadyAssignedIds.Any())
                {
                    response.SkippedApplicationIds.AddRange(alreadyAssignedIds);
                    response.Messages.Add(
                        $"Skipping {alreadyAssignedIds.Count} already-assigned applications");
                }

                // 6. Filter to only unassigned
                var unassignedApps = applications
                    .Where(a => !alreadyAssignedIds.Contains(a.Id))
                    .ToList();

                if (!unassignedApps.Any())
                {
                    response.FailureCount = applicationIds.Count;
                    response.Messages.Add("All selected applications are already assigned");
                    return response;
                }

                // 7. Create assignment records in memory (batch for efficiency)
                var newAssignments = new List<ApplicationAssignment>();
                var now = DateTime.UtcNow;

                foreach (var application in unassignedApps)
                {
                    var assignment = new ApplicationAssignment
                    {
                        Id = Guid.NewGuid(),
                        ApplicationId = application.Id,
                        ReviewerId = targetReviewerId,
                        AssignedBy = performedBy,
                        AssignedAt = now,
                        IsActive = true,
                        ReassignmentReason = reason  // Optional: store reason for audit
                    };

                    newAssignments.Add(assignment);
                    response.SuccessCount++;
                }

                // 8. Batch insert all at once (single database round-trip)
                if (newAssignments.Any())
                {
                    _context.ApplicationAssignments.AddRange(newAssignments);
                    await _context.SaveChangesAsync();

                    _logger.LogInformation(
                        $"Bulk assigned {newAssignments.Count} applications to reviewer " +
                        $"{targetReviewer.Id} ({targetReviewer.FirstName} {targetReviewer.LastName})");
                }

                // 9. Recalculate metrics to keep workload analytics in sync
                // Call RecalculateMetricsAsync for the target reviewer specifically
                _logger.LogInformation("Recalculating reviewer performance metrics after bulk assignment");
                await _performanceService.RecalculateMetricsAsync(targetReviewerId);

                // 10. Return success summary
                response.FailureCount = alreadyAssignedIds.Count;
                response.Messages.Add(
                    $"Successfully assigned {response.SuccessCount} applications " +
                    $"to {targetReviewer.FirstName} {targetReviewer.LastName}");

                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    $"Error in bulk assignment operation: {ex.Message}\n{ex.StackTrace}");

                response.FailureCount = applicationIds.Count;
                response.SuccessCount = 0;
                response.Messages.Add($"Bulk assignment failed: {ex.Message}");
                return response;
            }
        }

        /// <summary>
        /// Get all applications currently assigned to a reviewer.
        /// Only returns active assignments.
        /// </summary>
        public async Task<List<Application>> GetReviewerAssignmentsAsync(Guid reviewerId)
        {
            var assignments = await _context.ApplicationAssignments
                .Where(a => a.ReviewerId == reviewerId && a.IsActive)
                .Select(a => a.Application)
                .Where(app => app != null)
                .ToListAsync();

            return assignments ?? new List<Application>();
        }

        /// <summary>
        /// Get current workload (number of active assignments) for a reviewer.
        /// </summary>
        public async Task<int> GetReviewerWorkloadAsync(Guid reviewerId)
        {
            return await _context.ApplicationAssignments
                .Where(a => a.ReviewerId == reviewerId && a.IsActive)
                .CountAsync();
        }
    }
}
