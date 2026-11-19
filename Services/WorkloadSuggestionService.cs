using Microsoft.EntityFrameworkCore;
using MMGT.API.Data;
using MMGT.API.Services.Interfaces;

namespace MMGT.API.Services
{
    public class WorkloadSuggestionService : IWorkloadSuggestionService
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<WorkloadSuggestionService> _logger;
        private const double HighWorkloadThreshold = 0.75; // 75% of average

        public WorkloadSuggestionService(
            ApplicationDbContext context,
            ILogger<WorkloadSuggestionService> logger)
        {
            _context = context;
            _logger = logger;
        }

        private sealed class PlanPerson
        {
            public Guid Id;
            public string Name = string.Empty;
            public bool Available;
            public int Now;
            public int After;
            public int Next;
            public List<(Guid ApplicationId, string Ref)> Apps = new();
        }
        
        public async Task<WorkloadBalancingSummaryDto> GetWorkloadBalancingSuggestionsAsync()
        {
            try
            {
                var open = await _context.ApplicationAssignments
                    .AsNoTracking()
                    .Where(OpenWorkload.Open)
                    .Select(a => new { a.ApplicationId, a.ReviewerId, a.AssignedAt, Ref = a.Application.ApplicationNumber })
                    .ToListAsync();
        
                var reviewers = await _context.Users
                    .AsNoTracking()
                    .Where(u => u.IsActive && u.Role == "ADMIN")
                    .Select(u => new { u.Id, u.FirstName, u.LastName })
                    .ToListAsync();
        
                var notAvailable = new HashSet<Guid>(await _context.ReviewerAvailabilities
                    .AsNoTracking()
                    .Where(r => r.Status != "Available")
                    .Select(r => r.ReviewerId)
                    .ToListAsync());
        
                var byReviewer = open
                    .GroupBy(o => o.ReviewerId)
                    .ToDictionary(g => g.Key, g => g.OrderByDescending(x => x.AssignedAt).ToList());
        
                var people = new List<PlanPerson>();
                foreach (var u in reviewers)
                {
                    var person = new PlanPerson
                    {
                        Id = u.Id,
                        Name = ((u.FirstName ?? string.Empty) + " " + (u.LastName ?? string.Empty)).Trim(),
                        Available = !notAvailable.Contains(u.Id)
                    };
                    if (byReviewer.TryGetValue(u.Id, out var list))
                    {
                        foreach (var x in list)
                            person.Apps.Add((x.ApplicationId, x.Ref ?? "N/A"));
                    }
                    person.Now = person.Apps.Count;
                    person.After = person.Now;
                    people.Add(person);
                }
        
                var pool = people.Where(p => p.Available).ToList();
                var unavailable = people.Where(p => !p.Available).ToList();
                var totalOpen = open.Count;
                var poolOpen = pool.Sum(p => p.Now);
                var unavailableOpen = unavailable.Sum(p => p.Now);
                var otherOpen = totalOpen - poolOpen - unavailableOpen;
        
                var summary = new WorkloadBalancingSummaryDto
                {
                    TotalApplications = totalOpen,
                    AvailableReviewers = pool.Count,
                    IsBalanced = true
                };
        
                string Notes()
                {
                    var notes = string.Empty;
                    if (unavailable.Count > 0)
                        notes += $" {unavailable.Count} reviewer(s) are unavailable or on leave and hold {unavailableOpen} open application(s); they are not counted in the average and are not given new work.";
                    if (otherOpen > 0)
                        notes += $" {otherOpen} open application(s) are held by people who are not active reviewers.";
                    return notes;
                }
        
                if (pool.Count == 0)
                {
                    summary.Headline = "No reviewer is available right now, so there is nobody to move work to." + Notes();
                    return summary;
                }
        
                double avg = (double)poolOpen / pool.Count;
                int ceilAvg = (int)Math.Ceiling(avg);
                int floorAvg = (int)Math.Floor(avg);
                double margin = Math.Max(2, avg * 0.15);
        
                var donors = pool.Where(p => p.Now > avg + margin).OrderByDescending(p => p.Now).ToList();
                var takers = pool.Where(p => p.Now < avg - margin).OrderBy(p => p.Now).ToList();
        
                var moves = new List<WorkloadMoveDto>();
                var suggestions = new List<WorkloadSuggestionDto>();
                int di = 0, ti = 0;
                while (di < donors.Count && ti < takers.Count)
                {
                    var d = donors[di];
                    var t = takers[ti];
                    int surplus = d.After - ceilAvg;
                    int room = floorAvg - t.After;
                    if (surplus <= 0) { di++; continue; }
                    if (room <= 0) { ti++; continue; }
                    int n = Math.Min(Math.Min(surplus, room), d.Apps.Count - d.Next);
                    if (n <= 0) { di++; continue; }
        
                    var refs = new List<string>();
                    for (int i = 0; i < n; i++)
                    {
                        var app = d.Apps[d.Next++];
                        refs.Add(app.Ref);
                        suggestions.Add(new WorkloadSuggestionDto
                        {
                            ApplicationId = app.ApplicationId,
                            ApplicationRefNumber = app.Ref,
                            CurrentReviewerId = d.Id,
                            CurrentReviewerName = d.Name,
                            CurrentReviewerWorkload = d.Now,
                            SuggestedReviewerId = t.Id,
                            SuggestedReviewerName = t.Name,
                            SuggestedReviewerWorkload = t.Now,
                            RebalanceReason = $"The team average is {avg:F0} open applications per available reviewer. {d.Name} has {d.Now}; {t.Name} has {t.Now}.",
                            ImprovementPercentage = avg > 0 ? ((d.Now - avg) / avg) * 100 : 0
                        });
                    }
        
                    d.After -= n;
                    t.After += n;
                    moves.Add(new WorkloadMoveDto
                    {
                        FromReviewerId = d.Id,
                        FromReviewerName = d.Name,
                        FromOpenNow = d.Now,
                        ToReviewerId = t.Id,
                        ToReviewerName = t.Name,
                        ToOpenNow = t.Now,
                        Count = n,
                        ApplicationRefs = refs
                    });
                }
        
                summary.AverageWorkload = Math.Round(avg, 1);
                summary.MaxWorkload = pool.Max(p => p.Now);
                summary.MinWorkload = pool.Min(p => p.Now);
                summary.WorkloadStandardDeviation = CalculateStandardDeviation(pool.Select(p => (double)p.Now).ToList());
                summary.Suggestions = suggestions;
                summary.SuggestionsGenerated = suggestions.Count;
                summary.Moves = moves;
                summary.IsBalanced = moves.Count == 0;
                summary.ReviewerSummary = people
                    .OrderByDescending(p => p.Available)
                    .ThenByDescending(p => p.Now)
                    .Select(p => new WorkloadReviewerSummaryDto
                    {
                        ReviewerId = p.Id,
                        ReviewerName = p.Name,
                        OpenNow = p.Now,
                        OpenAfter = p.After,
                        Change = p.After - p.Now,
                        IsAvailable = p.Available
                    })
                    .ToList();
        
                if (moves.Count == 0)
                    summary.Headline = $"Work is spread fairly evenly: {pool.Count} available reviewer(s) hold about {avg:F0} open applications each." + Notes();
                else
                    summary.Headline = $"The team average is {avg:F0} open applications per available reviewer. {donors.Count} reviewer(s) are well above it and {takers.Count} are well below it. Moving {suggestions.Count} application(s) as suggested brings everyone close to the average." + Notes();
        
                return summary;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error generating workload balancing suggestions: {ex.Message}");
                throw;
            }
        }
        
        private async Task<WorkloadBalancingSummaryDto> GetWorkloadBalancingSuggestionsOldAsync()
        {
            try
            {
                var activeAssignments = await _context.ApplicationAssignments
                    .AsNoTracking()
                    .Where(OpenWorkload.Open)
                    .Include(a => a.Application)
                    .Include(a => a.Reviewer)
                    .ToListAsync();

                if (activeAssignments.Count == 0)
                {
                    return new WorkloadBalancingSummaryDto
                    {
                        TotalApplications = 0,
                        SuggestionsGenerated = 0,
                        IsBalanced = true
                    };
                }

                // Group by reviewer and count assignments
                var reviewerWorkloads = activeAssignments
                    .GroupBy(a => a.ReviewerId)
                    .Select(g => new
                    {
                        ReviewerId = g.Key,
                        ReviewerName = g.First().Reviewer?.FirstName + " " + g.First().Reviewer?.LastName,
                        Workload = g.Count(),
                        Assignments = g.ToList()
                    })
                    .ToList();

                double avgWorkload = reviewerWorkloads.Average(r => r.Workload);
                double maxWorkload = reviewerWorkloads.Max(r => r.Workload);
                double minWorkload = reviewerWorkloads.Min(r => r.Workload);
                double stdDev = CalculateStandardDeviation(reviewerWorkloads.Select(r => (double)r.Workload).ToList());

                var suggestions = GenerateSuggestions(reviewerWorkloads.Cast<dynamic>().ToList(), avgWorkload);

                bool isBalanced = stdDev <= (avgWorkload * 0.2);

                return new WorkloadBalancingSummaryDto
                {
                    TotalApplications = activeAssignments.Count,
                    SuggestionsGenerated = suggestions.Count,
                    AverageWorkload = avgWorkload,
                    MaxWorkload = maxWorkload,
                    MinWorkload = minWorkload,
                    WorkloadStandardDeviation = stdDev,
                    Suggestions = suggestions,
                    IsBalanced = isBalanced
                };
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error generating workload balancing suggestions: {ex.Message}");
                throw;
            }
        }

        public async Task<List<WorkloadSuggestionDto>> GetSuggestionsForReviewerAsync(Guid reviewerId)
        {
            var summary = await GetWorkloadBalancingSuggestionsAsync();
            return summary.Suggestions.Where(s => s.CurrentReviewerId == reviewerId).ToList();
        }
        
        private async Task<List<WorkloadSuggestionDto>> GetSuggestionsForReviewerOldAsync(Guid reviewerId)
        {
            try
            {
                var activeAssignments = await _context.ApplicationAssignments
                    .AsNoTracking()
                    .Where(OpenWorkload.Open)
                    .Include(a => a.Application)
                    .Include(a => a.Reviewer)
                    .ToListAsync();

                var reviewerWorkloads = activeAssignments
                    .GroupBy(a => a.ReviewerId)
                    .Select(g => new
                    {
                        ReviewerId = g.Key,
                        ReviewerName = g.First().Reviewer?.FirstName + " " + g.First().Reviewer?.LastName,
                        Workload = g.Count(),
                        Assignments = g.ToList()
                    })
                    .ToList();

                double avgWorkload = reviewerWorkloads.Average(r => r.Workload);
                var suggestions = GenerateSuggestions(reviewerWorkloads.Cast<dynamic>().ToList(), avgWorkload);

                return suggestions.Where(s => s.CurrentReviewerId == reviewerId).ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error getting suggestions for reviewer {reviewerId}: {ex.Message}");
                throw;
            }
        }

        public async Task<List<WorkloadSuggestionDto>> GetReassignmentCandidatesAsync(Guid reviewerId, int count = 5)
        {
            try
            {
                var reviewerAssignments = await _context.ApplicationAssignments
                    .AsNoTracking()
                    .Where(a => a.ReviewerId == reviewerId).Where(OpenWorkload.Open)
                    .Include(a => a.Application)
                    .Include(a => a.Reviewer)
                    .OrderBy(a => a.AssignedAt)
                    .Take(count)
                    .ToListAsync();

                var allAssignments = await _context.ApplicationAssignments
                    .AsNoTracking()
                    .Where(OpenWorkload.Open)
                    .Include(a => a.Reviewer)
                    .ToListAsync();

                var reviewerWorkloads = allAssignments
                    .GroupBy(a => a.ReviewerId)
                    .Select(g => new
                    {
                        ReviewerId = g.Key,
                        ReviewerName = g.First().Reviewer?.FirstName + " " + g.First().Reviewer?.LastName,
                        Workload = g.Count()
                    })
                    .OrderBy(r => r.Workload)
                    .ToList();

                double avgWorkload = reviewerWorkloads.Average(r => r.Workload);
                var leastBusyReviewer = reviewerWorkloads.FirstOrDefault();

                var suggestions = new List<WorkloadSuggestionDto>();

                foreach (var assignment in reviewerAssignments)
                {
                    var suggestion = new WorkloadSuggestionDto
                    {
                        ApplicationId = assignment.ApplicationId,
                        ApplicationRefNumber = assignment.Application?.ApplicationNumber ?? "N/A",
                        CurrentReviewerId = assignment.ReviewerId,
                        CurrentReviewerName = assignment.Reviewer?.FirstName + " " + assignment.Reviewer?.LastName,
                        CurrentReviewerWorkload = reviewerWorkloads.First(r => r.ReviewerId == reviewerId).Workload,
                        SuggestedReviewerId = leastBusyReviewer?.ReviewerId ?? Guid.Empty,
                        SuggestedReviewerName = leastBusyReviewer?.ReviewerName ?? "N/A",
                        SuggestedReviewerWorkload = leastBusyReviewer?.Workload ?? 0,
                        RebalanceReason = $"Reduce workload from {reviewerWorkloads.First(r => r.ReviewerId == reviewerId).Workload} to {avgWorkload:F0}",
                        ImprovementPercentage = ((reviewerWorkloads.First(r => r.ReviewerId == reviewerId).Workload - avgWorkload) / avgWorkload) * 100
                    };

                    suggestions.Add(suggestion);
                }

                return suggestions;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error getting reassignment candidates for reviewer {reviewerId}: {ex.Message}");
                throw;
            }
        }

        public async Task<bool> IsWorkloadBalancedAsync(double tolerancePercentage = 20)
        {
            try
            {
                var (stdDev, _) = await GetWorkloadImbalanceMetricsAsync();

                var activeAssignments = await _context.ApplicationAssignments
                    .AsNoTracking()
                    .Where(OpenWorkload.Open)
                    .CountAsync();

                var reviewerCount = await _context.ApplicationAssignments
                    .AsNoTracking()
                    .Where(OpenWorkload.Open)
                    .Select(a => a.ReviewerId)
                    .Distinct()
                    .CountAsync();

                if (reviewerCount == 0) return true;

                double avgWorkload = (double)activeAssignments / reviewerCount;
                double threshold = avgWorkload * (tolerancePercentage / 100);

                return stdDev <= threshold;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error checking workload balance: {ex.Message}");
                throw;
            }
        }

        public async Task<(double StdDev, double CoeffVariation)> GetWorkloadImbalanceMetricsAsync()
        {
            try
            {
                var reviewerWorkloads = await _context.ApplicationAssignments
                    .AsNoTracking()
                    .Where(OpenWorkload.Open)
                    .GroupBy(a => a.ReviewerId)
                    .Select(g => new { ReviewerId = g.Key, Workload = g.Count() })
                    .ToListAsync();

                if (reviewerWorkloads.Count == 0)
                    return (0, 0);

                var workloads = reviewerWorkloads.Select(r => (double)r.Workload).ToList();
                double stdDev = CalculateStandardDeviation(workloads);
                double mean = workloads.Average();
                double coeffVariation = mean > 0 ? (stdDev / mean) : 0;

                return (stdDev, coeffVariation);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error calculating workload imbalance metrics: {ex.Message}");
                throw;
            }
        }

        private List<WorkloadSuggestionDto> GenerateSuggestions(
            List<dynamic> reviewerWorkloads,
            double avgWorkload)
        {
            var suggestions = new List<WorkloadSuggestionDto>();

            var overloadedReviewers = reviewerWorkloads
                .Where(r => r.Workload > avgWorkload * HighWorkloadThreshold)
                .OrderByDescending(r => r.Workload)
                .ToList();

            var underloadedReviewers = reviewerWorkloads
                .Where(r => r.Workload < avgWorkload * 0.5)
                .OrderBy(r => r.Workload)
                .ToList();

            foreach (var overloaded in overloadedReviewers)
            {
                var underloaded = underloadedReviewers.FirstOrDefault();
                if (underloaded == null) break;

                var reassignmentCount = Math.Max(1, (int)(overloaded.Workload - avgWorkload) / 2);

                for (int i = 0; i < reassignmentCount && i < overloaded.Assignments.Count; i++)
                {
                    var assignment = overloaded.Assignments[i];

                    var suggestion = new WorkloadSuggestionDto
                    {
                        ApplicationId = assignment.ApplicationId,
                        ApplicationRefNumber = assignment.Application?.ApplicationNumber ?? "N/A",
                        CurrentReviewerId = overloaded.ReviewerId,
                        CurrentReviewerName = overloaded.ReviewerName,
                        CurrentReviewerWorkload = overloaded.Workload,
                        SuggestedReviewerId = underloaded.ReviewerId,
                        SuggestedReviewerName = underloaded.ReviewerName,
                        SuggestedReviewerWorkload = underloaded.Workload,
                        RebalanceReason = $"Reduce overloaded reviewer from {overloaded.Workload} to {avgWorkload:F0} items",
                        ImprovementPercentage = ((overloaded.Workload - avgWorkload) / avgWorkload) * 100
                    };

                    suggestions.Add(suggestion);
                }
            }

            return suggestions;
        }

        private double CalculateStandardDeviation(List<double> values)
        {
            if (values.Count == 0) return 0;

            double mean = values.Average();
            double sumOfSquaresOfDifferences = values.Sum(val => Math.Pow(val - mean, 2));
            double standardDeviation = Math.Sqrt(sumOfSquaresOfDifferences / values.Count);

            return standardDeviation;
        }
    }
}
