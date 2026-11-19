using Microsoft.EntityFrameworkCore;
using MMGT.API.Data;
using MMGT.API.Models;
using MMGT.API.Services.Interfaces;

namespace MMGT.API.Services
{
    /// <summary>
    /// Balances open workload between available reviewers.
    /// Workload = open items (see OpenWorkload). Only applications still in 'submitted' (not started by the reviewer) are moved
    /// when balancing, oldest first. Every move closes the old assignment, creates a new one and writes a history row.
    /// </summary>
    public class CriteriaBasedReassignmentService : ICriteriaBasedReassignmentService
    {
        private readonly ApplicationDbContext _context;
        private readonly IReviewerAvailabilityService _availabilityService;
        private readonly ILogger<CriteriaBasedReassignmentService> _logger;

        public CriteriaBasedReassignmentService(
            ApplicationDbContext context,
            IReviewerAvailabilityService availabilityService,
            ILogger<CriteriaBasedReassignmentService> logger)
        {
            _context = context;
            _availabilityService = availabilityService;
            _logger = logger;
        }

        private sealed class PoolReviewer
        {
            public Guid Id { get; set; }
            public string Name { get; set; } = string.Empty;
            public int Open { get; set; }
        }

        /// <summary>Active reviewers (ADMIN role) who are available now, with their open workload. Idle reviewers are included.</summary>
        private async Task<List<PoolReviewer>> GetAvailablePoolAsync()
        {
            var users = await _context.Users.AsNoTracking()
                .Where(u => u.IsActive && u.Role == "ADMIN")
                .ToListAsync();

            var open = await _context.ApplicationAssignments
                .Where(OpenWorkload.Open)
                .GroupBy(a => a.ReviewerId)
                .Select(g => new { ReviewerId = g.Key, Count = g.Count() })
                .ToListAsync();

            var pool = new List<PoolReviewer>();
            foreach (var u in users)
            {
                if (!await _availabilityService.IsReviewerAvailableAsync(u.Id)) continue;
                pool.Add(new PoolReviewer
                {
                    Id = u.Id,
                    Name = (u.FirstName + " " + u.LastName).Trim(),
                    Open = open.FirstOrDefault(o => o.ReviewerId == u.Id)?.Count ?? 0
                });
            }
            return pool;
        }

        public async Task<List<CriteriaReassignmentResultDto>> ExecuteReassignmentsByCriteriaAsync(ReassignmentCriteriaDto criteria, Guid? performedBy = null)
        {
            var results = new List<CriteriaReassignmentResultDto>();
            try
            {
                var pool = await GetAvailablePoolAsync();
                if (pool.Count < 2)
                {
                    _logger.LogInformation("Balancing skipped: fewer than two available reviewers");
                    return results;
                }

                int maxMoves = Math.Clamp(criteria.MaxReassignments, 1, 200);
                int tolerance = Math.Max(1, criteria.MaxDifference);
                var exhausted = new HashSet<Guid>();
                var plannedAppIds = new List<Guid>();

                while (results.Count < maxMoves)
                {
                    var high = pool.Where(p => !exhausted.Contains(p.Id)).OrderByDescending(p => p.Open).FirstOrDefault();
                    var low = pool.OrderBy(p => p.Open).First();
                    if (high == null || high.Id == low.Id || high.Open - low.Open <= tolerance) break;

                    // Only work the reviewer has not started yet, oldest first.
                    var candidate = await _context.ApplicationAssignments
                        .Include(a => a.Application)
                        .Where(a => a.ReviewerId == high.Id)
                        .Where(OpenWorkload.Open)
                        .Where(a => a.Application!.Status == "submitted" && !plannedAppIds.Contains(a.ApplicationId))
                        .OrderBy(a => a.AssignedAt)
                        .FirstOrDefaultAsync();

                    if (candidate == null)
                    {
                        exhausted.Add(high.Id);
                        continue;
                    }

                    plannedAppIds.Add(candidate.ApplicationId);
                    var details = $"Workload balancing: {high.Name} had {high.Open} open items, {low.Name} had {low.Open}";

                    CriteriaReassignmentResultDto result;
                    if (criteria.PreviewOnly)
                    {
                        result = new CriteriaReassignmentResultDto
                        {
                            ApplicationId = candidate.ApplicationId,
                            ApplicationRefNumber = candidate.Application?.ApplicationNumber,
                            PreviousReviewerId = high.Id,
                            PreviousReviewerName = high.Name,
                            NewReviewerId = low.Id,
                            NewReviewerName = low.Name,
                            ReassignmentReason = details,
                            Success = true
                        };
                    }
                    else
                    {
                        result = await MoveAsync(candidate, low.Id, low.Name, high.Name, "CriteriaBased", details, performedBy);
                    }

                    results.Add(result);
                    if (result.Success) { high.Open--; low.Open++; }
                    else { exhausted.Add(high.Id); }
                }

                _logger.LogInformation($"Balancing {(criteria.PreviewOnly ? "preview" : "run")}: {results.Count(r => r.Success)} successful, {results.Count(r => !r.Success)} failed");
                return results;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error balancing workload: {ex.Message}");
                throw;
            }
        }

        public async Task<List<CriteriaReassignmentResultDto>> GetReassignmentCandidatesByCriteriaAsync(Guid reviewerId, ReassignmentCriteriaDto criteria)
        {
            var results = new List<CriteriaReassignmentResultDto>();
            var pool = await GetAvailablePoolAsync();
            var me = pool.FirstOrDefault(p => p.Id == reviewerId);
            if (me == null || pool.Count < 2) return results;

            int tolerance = Math.Max(1, criteria.MaxDifference);
            int maxMoves = Math.Clamp(criteria.MaxReassignments, 1, 200);

            var items = await _context.ApplicationAssignments
                .AsNoTracking()
                .Include(a => a.Application)
                .Where(a => a.ReviewerId == reviewerId)
                .Where(OpenWorkload.Open)
                .Where(a => a.Application!.Status == "submitted")
                .OrderBy(a => a.AssignedAt)
                .ToListAsync();

            foreach (var item in items)
            {
                if (results.Count >= maxMoves) break;
                var low = pool.Where(p => p.Id != reviewerId).OrderBy(p => p.Open).First();
                if (me.Open - low.Open <= tolerance) break;

                results.Add(new CriteriaReassignmentResultDto
                {
                    ApplicationId = item.ApplicationId,
                    ApplicationRefNumber = item.Application?.ApplicationNumber,
                    PreviousReviewerId = reviewerId,
                    PreviousReviewerName = me.Name,
                    NewReviewerId = low.Id,
                    NewReviewerName = low.Name,
                    ReassignmentReason = $"{me.Name} has {me.Open} open items, {low.Name} has {low.Open}",
                    Success = true
                });
                me.Open--; low.Open++;
            }
            return results;
        }

        public async Task<List<CriteriaReassignmentResultDto>> ReassignFromUnavailableReviewersAsync()
        {
            var results = new List<CriteriaReassignmentResultDto>();
            try
            {
                var unavailable = await _availabilityService.GetUnavailableReviewersAsync();
                var unavailableIds = unavailable.Select(r => r.ReviewerId).ToList();
                if (!unavailableIds.Any()) return results;

                var pool = (await GetAvailablePoolAsync()).Where(p => !unavailableIds.Contains(p.Id)).ToList();
                if (pool.Count == 0)
                {
                    _logger.LogWarning("No available reviewer to take work from unavailable reviewers");
                    return results;
                }

                var items = await _context.ApplicationAssignments
                    .Include(a => a.Application)
                    .Include(a => a.Reviewer)
                    .Where(a => unavailableIds.Contains(a.ReviewerId))
                    .Where(OpenWorkload.Open)
                    .OrderBy(a => a.AssignedAt)
                    .ToListAsync();

                foreach (var item in items)
                {
                    var target = pool.OrderBy(p => p.Open).First();
                    var previousName = ((item.Reviewer?.FirstName ?? "") + " " + (item.Reviewer?.LastName ?? "")).Trim();
                    var result = await MoveAsync(item, target.Id, target.Name, previousName, "AvailabilityChange", "Original reviewer unavailable", null);
                    results.Add(result);
                    if (result.Success) target.Open++;
                }

                _logger.LogInformation($"Reassigned {results.Count(r => r.Success)} applications from unavailable reviewers");
                return results;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error reassigning from unavailable reviewers: {ex.Message}");
                throw;
            }
        }

        public async Task<List<CriteriaReassignmentResultDto>> AutoBalanceWorkloadAsync(Guid? performedBy = null)
        {
            var criteria = new ReassignmentCriteriaDto { MaxReassignments = 50, MaxDifference = 3 };
            return await ExecuteReassignmentsByCriteriaAsync(criteria, performedBy);
        }

        public async Task<(bool IsValid, string Reason)> ValidateReassignmentAsync(Guid applicationId, Guid currentReviewerId, Guid newReviewerId)
        {
            try
            {
                var application = await _context.Applications.FirstOrDefaultAsync(a => a.Id == applicationId);
                if (application == null) return (false, "Application not found");
                if (application.IsLocked) return (false, "Application is locked");

                var currentAssignment = await _context.ApplicationAssignments
                    .FirstOrDefaultAsync(a => a.ApplicationId == applicationId && a.ReviewerId == currentReviewerId && a.IsActive);
                if (currentAssignment == null) return (false, "Current assignment not found");

                var newReviewer = await _context.Users.FirstOrDefaultAsync(u => u.Id == newReviewerId);
                if (newReviewer == null || !newReviewer.IsActive) return (false, "New reviewer not found or inactive");

                if (!await _availabilityService.IsReviewerAvailableAsync(newReviewerId))
                    return (false, "New reviewer is currently unavailable");

                if (newReviewer.Role != "ADMIN") return (false, "Target reviewer must have ADMIN role");

                return (true, "Reassignment is valid");
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error validating reassignment: {ex.Message}");
                return (false, $"Validation error: {ex.Message}");
            }
        }

        private static string Cut(string text, int max) => text.Length <= max ? text : text.Substring(0, max);

        /// <summary>Close the old assignment, create a new one and write a history row, all in one transaction.</summary>
        private async Task<CriteriaReassignmentResultDto> MoveAsync(
            ApplicationAssignment old, Guid newReviewerId, string newName, string previousName,
            string reason, string details, Guid? performedBy)
        {
            var dto = new CriteriaReassignmentResultDto
            {
                ApplicationId = old.ApplicationId,
                ApplicationRefNumber = old.Application?.ApplicationNumber,
                PreviousReviewerId = old.ReviewerId,
                PreviousReviewerName = previousName,
                NewReviewerId = newReviewerId,
                NewReviewerName = newName,
                ReassignmentReason = details
            };

            try
            {
                if (old.Application == null || old.Application.IsLocked)
                {
                    dto.Success = false;
                    dto.ErrorMessage = "Application is locked or missing";
                    return dto;
                }

                var previousReviewerId = old.ReviewerId;
                using var tx = await _context.Database.BeginTransactionAsync();

                old.IsActive = false;
                var created = new ApplicationAssignment
                {
                    Id = Guid.NewGuid(),
                    ApplicationId = old.ApplicationId,
                    ReviewerId = newReviewerId,
                    AssignedBy = performedBy,
                    AssignedAt = DateTime.UtcNow,
                    ReassignmentReason = Cut(details, 500),
                    IsActive = true
                };
                _context.ApplicationAssignments.Add(created);
                await _context.SaveChangesAsync();

                _context.ReassignmentHistories.Add(new ReassignmentHistory
                {
                    ApplicationAssignmentId = created.Id,
                    ApplicationId = old.ApplicationId,
                    PreviousReviewerId = previousReviewerId,
                    NewReviewerId = newReviewerId,
                    ReassignmentReason = Cut(reason, 50),
                    ReassignmentDetails = Cut(details, 500),
                    ReassignedAt = DateTime.UtcNow,
                    ReassignedByUserId = performedBy,
                    PreviousAssignmentStatus = "Active",
                    NewAssignmentStatus = "Active",
                    IsCompleted = true
                });
                await _context.SaveChangesAsync();
                await tx.CommitAsync();

                dto.Success = true;
                return dto;
            }
            catch (Exception ex)
            {
                _context.ChangeTracker.Clear();
                _logger.LogError($"Error moving application {old.ApplicationId}: {ex.Message}");
                dto.Success = false;
                dto.ErrorMessage = ex.Message;
                return dto;
            }
        }
    }
}