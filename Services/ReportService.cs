using MMGT.API.Services.Interfaces;
using MMGT.API.DTOs.Application;
using MMGT.API.DTOs.Report;
using MMGT.API.Data;
using Microsoft.EntityFrameworkCore;
using OfficeOpenXml;

namespace MMGT.API.Services
{
    public class ReportService : IReportService
    {
        private readonly ApplicationDbContext _context;

        public ReportService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<ReportResponse<ApplicationReportDto>> GenerateApplicationReportAsync(ReportFilterRequest filter)
        {
            filter.Validate();

            var query = BuildFilteredQuery(filter);
            query = ApplySorting(query, filter.SortBy, filter.SortDescending);

            // Get total count once (single query)
            int totalCount = await query.CountAsync();

            // Get paginated data and summary sequentially (DbContext is not thread-safe for concurrent operations)
            var data = await GetReportDataAsync(filter);
            await EnrichDecisionAsync(data);
            var summary = await GetReportSummaryAsync(filter);

            var totalPages = (int)Math.Ceiling((double)totalCount / filter.PageSize);
            var hasNextPage = filter.PageNumber < totalPages;
            var hasPreviousPage = filter.PageNumber > 1;

            return new ReportResponse<ApplicationReportDto>
            {
                Data = data,
                TotalCount = totalCount,
                PageNumber = filter.PageNumber,
                PageSize = filter.PageSize,
                TotalPages = totalPages,
                HasNextPage = hasNextPage,
                HasPreviousPage = hasPreviousPage,
                Summary = summary
            };
        }

        private async Task<List<ApplicationReportDto>> GetReportDataAsync(ReportFilterRequest filter)
        {
            var query = BuildFilteredQuery(filter);
            query = ApplySorting(query, filter.SortBy, filter.SortDescending);

            // Step 1: Get paginated IDs directly from the sorted query
            var paginatedIds = await query
                .Select(a => a.Id)
                .Skip((filter.PageNumber - 1) * filter.PageSize)
                .Take(filter.PageSize)
                .ToListAsync();

            if (paginatedIds.Count == 0)
                return new List<ApplicationReportDto>();

            // Step 2: Load only paginated Applications
            var paginatedIdHashSet = new HashSet<Guid>(paginatedIds);
            var applications = await _context.Applications
                .AsNoTracking()
                .Where(a => paginatedIdHashSet.Contains(a.Id))
                .ToListAsync();

            // Step 3: Load PMI Companies
            var pmiCompanyIds = applications.Select(a => a.PmiCompanyId).Distinct().ToList();
            var pmiCompanyIdHashSet = new HashSet<Guid>(pmiCompanyIds);
            var pmiCompaniesDict = await _context.PMICompanies
                .AsNoTracking()
                .Where(p => pmiCompanyIdHashSet.Contains(p.Id))
                .ToDictionaryAsync(p => p.Id, p => p.PmiName);

            // Step 4: Get document counts
            var documentCountsHashSet = new HashSet<Guid>(paginatedIds);
            var documentCounts = await _context.ApplicationDocuments
                .AsNoTracking()
                .Where(d => documentCountsHashSet.Contains(d.ApplicationId) && d.IsLatestVersion)
                .GroupBy(d => d.ApplicationId)
                .Select(g => new { ApplicationId = g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.ApplicationId, x => x.Count);

            // Step 5: Get assigned reviewers (current active assignment)
            var reviewerAssignments = await _context.ApplicationAssignments
                .AsNoTracking()
                .Where(a => paginatedIdHashSet.Contains(a.ApplicationId) && a.IsActive)
                .Include(a => a.Reviewer)
                .ToListAsync();

            var reviewerDict = reviewerAssignments
                .GroupBy(a => a.ApplicationId)
                .ToDictionary(g => g.Key, g => g.OrderByDescending(x => x.AssignedAt).First().Reviewer);

            var assignmentDateDict = reviewerAssignments
                .GroupBy(a => a.ApplicationId)
                .ToDictionary(g => g.Key, g => g.OrderByDescending(x => x.AssignedAt).First().AssignedAt);

            // Step 6: Combine with O(1) lookups
            return applications.Select(a => new ApplicationReportDto
            {
                ApplicationNumber = a.ApplicationNumber,
                RsaPin = a.RsaPin,
                ApplicantName = $"{a.FirstName} {a.LastName}".Trim(),
                PmiCompany = pmiCompaniesDict.TryGetValue(a.PmiCompanyId, out var pmiName) ? pmiName : string.Empty,
                PropertyValue = a.PropertyValue,
                LoanAmount = a.LoanAmount,
                Status = a.Status,
                CreatedAt = a.CreatedAt,
                SubmittedAt = a.SubmittedAt,
                DocumentCount = documentCounts.TryGetValue(a.Id, out var count) ? count : 0,
                AssignedReviewerName = reviewerDict.TryGetValue(a.Id, out var reviewer) && reviewer != null 
                    ? $"{reviewer.FirstName} {reviewer.LastName}".Trim() 
                    : null,
                AssignedReviewerEmail = reviewerDict.TryGetValue(a.Id, out var reviewer2) && reviewer2 != null 
                    ? reviewer2.Email 
                    : null,
                AssignedAt = assignmentDateDict.TryGetValue(a.Id, out var assignedDate) ? assignedDate : null
            }).ToList();
        }

        public async Task<ReportResponse<ApplicationDto>> GenerateApplicationReportFullAsync(ReportFilterRequest filter)
        {
            filter.Validate();

            var query = BuildFilteredQuery(filter);
            query = ApplySorting(query, filter.SortBy, filter.SortDescending);

            // Get total count once (single query)
            int totalCount = await query.CountAsync();

            // Get paginated data and summary sequentially (DbContext is not thread-safe for concurrent operations)
            var data = await GetReportFullDataAsync(filter);
            var summary = await GetReportSummaryAsync(filter);

            var totalPages = (int)Math.Ceiling((double)totalCount / filter.PageSize);
            var hasNextPage = filter.PageNumber < totalPages;
            var hasPreviousPage = filter.PageNumber > 1;

            return new ReportResponse<ApplicationDto>
            {
                Data = data,
                TotalCount = totalCount,
                PageNumber = filter.PageNumber,
                PageSize = filter.PageSize,
                TotalPages = totalPages,
                HasNextPage = hasNextPage,
                HasPreviousPage = hasPreviousPage,
                Summary = summary
            };
        }

        private async Task<List<ApplicationDto>> GetReportFullDataAsync(ReportFilterRequest filter)
        {
            var query = BuildFilteredQuery(filter);
            query = ApplySorting(query, filter.SortBy, filter.SortDescending);

            // Step 1: Get paginated IDs directly from the sorted query
            var paginatedIds = await query
                .Select(a => a.Id)
                .Skip((filter.PageNumber - 1) * filter.PageSize)
                .Take(filter.PageSize)
                .ToListAsync();

            if (paginatedIds.Count == 0)
                return new List<ApplicationDto>();

            // Step 2: Load only paginated Applications
            var paginatedIdHashSet = new HashSet<Guid>(paginatedIds);
            var applications = await _context.Applications
                .AsNoTracking()
                .Where(a => paginatedIdHashSet.Contains(a.Id))
                .ToListAsync();

            // Step 3: Extract IDs for related data
            var userIds = applications.Select(a => a.UserId).Distinct().ToList();
            var pmiCompanyIds = applications.Select(a => a.PmiCompanyId).Distinct().ToList();

            // Step 4: Load Users, PMI Companies, and document counts sequentially (DbContext is not thread-safe)
            var userIdHashSet = new HashSet<Guid>(userIds);
            var usersDict = await _context.Users
                .AsNoTracking()
                .Where(u => userIdHashSet.Contains(u.Id))
                .ToDictionaryAsync(u => u.Id, u => new { u.FirstName, u.LastName });

            var pmiCompanyIdHashSet = new HashSet<Guid>(pmiCompanyIds);
            var pmiCompaniesDict = await _context.PMICompanies
                .AsNoTracking()
                .Where(p => pmiCompanyIdHashSet.Contains(p.Id))
                .ToDictionaryAsync(p => p.Id, p => p.PmiName);

            var documentCountsHashSet = new HashSet<Guid>(paginatedIds);
            var documentCounts = await _context.ApplicationDocuments
                .AsNoTracking()
                .Where(d => documentCountsHashSet.Contains(d.ApplicationId) && d.IsLatestVersion)
                .GroupBy(d => d.ApplicationId)
                .Select(g => new { ApplicationId = g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.ApplicationId, x => x.Count);

            // Step 5: Get assigned reviewers (current active assignment)
            var reviewerAssignments = await _context.ApplicationAssignments
                .AsNoTracking()
                .Where(a => paginatedIdHashSet.Contains(a.ApplicationId) && a.IsActive)
                .Include(a => a.Reviewer)
                .ToListAsync();

            var reviewerDict = reviewerAssignments
                .GroupBy(a => a.ApplicationId)
                .ToDictionary(g => g.Key, g => g.OrderByDescending(x => x.AssignedAt).First().Reviewer);

            var assignmentDateDict = reviewerAssignments
                .GroupBy(a => a.ApplicationId)
                .ToDictionary(g => g.Key, g => g.OrderByDescending(x => x.AssignedAt).First().AssignedAt);

            // Step 6: Combine with O(1) lookups
            return applications.Select(a => new ApplicationDto
            {
                Id = a.Id,
                ApplicationNumber = a.ApplicationNumber,
                Status = a.Status,
                Priority = a.Priority,
                FirstName = a.FirstName,
                MiddleName = a.MiddleName,
                LastName = a.LastName,
                RsaPin = a.RsaPin,
                EquityValue = a.EquityValue,
                SupplementaryValue = a.SupplementaryValue,
                LoanAmount = a.LoanAmount,
                PropertyValue = a.PropertyValue,
                PmiRepName = a.PmiRepName,
                PmiRepEmail = a.PmiRepEmail,
                PmiCompanyName = a.PmiCompanyName,
                CreatedAt = a.CreatedAt,
                UpdatedAt = a.UpdatedAt,
                SubmittedAt = a.SubmittedAt,
                UserId = a.UserId,
                UserName = usersDict.TryGetValue(a.UserId, out var user) 
                    ? $"{user.FirstName} {user.LastName}".Trim() 
                    : string.Empty,
                PmiCompanyId = a.PmiCompanyId,
                CompanyName = pmiCompaniesDict.TryGetValue(a.PmiCompanyId, out var companyName) ? companyName : string.Empty,
                DocumentCount = documentCounts.TryGetValue(a.Id, out var count) ? count : 0,
                AssignedReviewerName = reviewerDict.TryGetValue(a.Id, out var reviewer) && reviewer != null 
                    ? $"{reviewer.FirstName} {reviewer.LastName}".Trim() 
                    : null,
                AssignedReviewerEmail = reviewerDict.TryGetValue(a.Id, out var reviewer2) && reviewer2 != null 
                    ? reviewer2.Email 
                    : null,
                AssignedAt = assignmentDateDict.TryGetValue(a.Id, out var assignedDate) ? assignedDate : null
            }).ToList();
        }

        public async Task<ReportSummaryDto> GetReportSummaryAsync(ReportFilterRequest filter)
        {
            filter.Validate();

            // Build separate queries for each aggregation (cannot reuse the same query object)

            // Count total records
            var countQuery = BuildFilteredQuery(filter);
            var totalCount = await countQuery.CountAsync();

            // Get status counts
            var statusCountQuery = BuildFilteredQuery(filter);
            var statusCounts = await statusCountQuery
                .GroupBy(a => a.Status.ToLower())
                .Select(g => new { Status = g.Key, Count = g.Count() })
                .ToListAsync();

            // Get total loan amount
            var loanQuery = BuildFilteredQuery(filter);
            var totalLoanAmount = await loanQuery.SumAsync(a => a.LoanAmount);

            // Get total property value
            var propertyQuery = BuildFilteredQuery(filter);
            var totalPropertyValue = await propertyQuery.SumAsync(a => a.PropertyValue);

            var summary = new ReportSummaryDto
            {
                TotalRecords = totalCount,
                SubmittedCount = statusCounts.FirstOrDefault(s => s.Status == "submitted")?.Count ?? 0,
                UnderReviewCount = statusCounts.FirstOrDefault(s => s.Status == "under_review")?.Count ?? 0,
                PendingCount = statusCounts.FirstOrDefault(s => s.Status == "pending")?.Count ?? 0,
                RejectedCount = statusCounts.FirstOrDefault(s => s.Status == "rejected")?.Count ?? 0,
                ReturnedCount = statusCounts.FirstOrDefault(s => s.Status == "returned")?.Count ?? 0,
                ApprovedCount = statusCounts.FirstOrDefault(s => s.Status == "approved")?.Count ?? 0,
                CompletedCount = statusCounts.FirstOrDefault(s => s.Status == "completed")?.Count ?? 0,
                TotalLoanAmount = totalLoanAmount,
                TotalPropertyValue = totalPropertyValue
            };

            return summary;
        }

        /// <summary>
        /// Fills decision date, turnaround days and (for rejected applications) the rejection reason, date and reviewer.
        /// The decision is the latest review-history row that matches the application's current status.
        /// </summary>
        private async Task EnrichDecisionAsync(List<ApplicationReportDto> rows, IQueryable<Guid>? scope = null)
        {
            if (rows.Count == 0)
                return;
        
            var numbers = new HashSet<string>(rows.Select(r => r.ApplicationNumber));
            IQueryable<Models.Application> appsScope = _context.Applications.AsNoTracking();
            appsScope = scope != null ? appsScope.Where(a => scope.Contains(a.Id)) : appsScope.Where(a => numbers.Contains(a.ApplicationNumber));
            var apps = await appsScope
                .Select(a => new { a.Id, a.ApplicationNumber, a.Status, a.SubmittedAt, a.CompletedAt })
                .ToListAsync();
        
            var byNumber = apps.GroupBy(a => a.ApplicationNumber).ToDictionary(g => g.Key, g => g.First());
            var ids = new HashSet<Guid>(apps.Select(a => a.Id));
        
            IQueryable<Models.ApplicationReviewHistory> historyScope = _context.Set<Models.ApplicationReviewHistory>().AsNoTracking();
            historyScope = scope != null ? historyScope.Where(h => scope.Contains(h.ApplicationId)) : historyScope.Where(h => ids.Contains(h.ApplicationId));
            var history = await historyScope
                .Where(h => (h.Status.ToLower() == "approved" || h.Status.ToLower() == "rejected" || h.Status.ToLower() == "returned"))
                .Select(h => new { h.ApplicationId, Status = h.Status.ToLower(), h.Comments, h.CreatedAt, h.ReviewedById })
                .ToListAsync();
        
            var historyByApp = history
                .GroupBy(h => h.ApplicationId)
                .ToDictionary(g => g.Key, g => g.OrderByDescending(h => h.CreatedAt).ToList());
        
            var reviewerIds = new HashSet<Guid>(history.Select(h => h.ReviewedById));
            var reviewerNames = await _context.Users
                .AsNoTracking()
                .Where(u => reviewerIds.Contains(u.Id))
                .ToDictionaryAsync(u => u.Id, u => u.FirstName + " " + u.LastName);
        
            foreach (var r in rows)
            {
                if (!byNumber.TryGetValue(r.ApplicationNumber, out var app))
                    continue;
        
                r.Id = app.Id;
                var status = (app.Status ?? string.Empty).ToLowerInvariant();
                historyByApp.TryGetValue(app.Id, out var list);
                var decision = list?.FirstOrDefault(h => h.Status == status);
        
                DateTime? decisionAt = decision?.CreatedAt;
                if (decisionAt == null && (status == "approved" || status == "rejected"))
                    decisionAt = app.CompletedAt;
        
                r.DecisionAt = decisionAt;
                if (decisionAt.HasValue && app.SubmittedAt.HasValue && decisionAt.Value >= app.SubmittedAt.Value)
                    r.TurnaroundDays = Math.Round((decisionAt.Value - app.SubmittedAt.Value).TotalDays, 1);
        
                if (status == "rejected")
                {
                    r.RejectionReason = decision?.Comments;
                    r.RejectedAt = decision?.CreatedAt;
                    r.RejectedBy = decision != null && reviewerNames.TryGetValue(decision.ReviewedById, out var rejectedByName)
                        ? rejectedByName.Trim()
                        : null;
                }
            }
        }
        
        public async Task<ReportAnalyticsDto> GetReportAnalyticsAsync(ReportFilterRequest filter)
        {
            filter.Validate();
            var result = new ReportAnalyticsDto();
        
            // By PMI company (sequential queries: DbContext is not thread-safe)
            var pmiRows = await BuildFilteredQuery(filter)
                .GroupBy(a => a.PmiCompanyId)
                .Select(g => new
                {
                    PmiCompanyId = g.Key,
                    Total = g.Count(),
                    Submitted = g.Count(x => x.Status.ToLower() == "submitted"),
                    UnderReview = g.Count(x => x.Status.ToLower() == "under_review"),
                    Pending = g.Count(x => x.Status.ToLower() == "pending"),
                    Approved = g.Count(x => x.Status.ToLower() == "approved"),
                    Rejected = g.Count(x => x.Status.ToLower() == "rejected"),
                    Returned = g.Count(x => x.Status.ToLower() == "returned"),
                    Completed = g.Count(x => x.Status.ToLower() == "completed")
                })
                .ToListAsync();
        
            var pmiNameDict = await _context.PMICompanies.AsNoTracking().ToDictionaryAsync(p => p.Id, p => p.PmiName);
            result.ByPmi = pmiRows
                .OrderByDescending(p => p.Total)
                .Select(p => new PmiBreakdownRowDto
                {
                    PmiCompany = pmiNameDict.TryGetValue(p.PmiCompanyId, out var pmiDisplay) ? pmiDisplay : "Unknown",
                    Total = p.Total, Submitted = p.Submitted, UnderReview = p.UnderReview, Pending = p.Pending,
                    Approved = p.Approved, Rejected = p.Rejected, Returned = p.Returned, Completed = p.Completed
                })
                .ToList();
        
            // By reviewer: applications currently assigned to each reviewer
            var filtered = BuildFilteredQuery(filter);
            var reviewerRows = await _context.ApplicationAssignments
                .AsNoTracking()
                .Where(x => x.IsActive)
                .Join(filtered, x => x.ApplicationId, a => a.Id, (x, a) => new { x.ReviewerId, a.Status })
                .GroupBy(r => r.ReviewerId)
                .Select(g => new
                {
                    ReviewerId = g.Key,
                    Total = g.Count(),
                    Submitted = g.Count(x => x.Status.ToLower() == "submitted"),
                    UnderReview = g.Count(x => x.Status.ToLower() == "under_review"),
                    Pending = g.Count(x => x.Status.ToLower() == "pending"),
                    Approved = g.Count(x => x.Status.ToLower() == "approved"),
                    Rejected = g.Count(x => x.Status.ToLower() == "rejected"),
                    Returned = g.Count(x => x.Status.ToLower() == "returned"),
                    Completed = g.Count(x => x.Status.ToLower() == "completed")
                })
                .ToListAsync();
        
            var reviewerIdSet = new HashSet<Guid>(reviewerRows.Select(r => r.ReviewerId));
            var reviewerNameDict = await _context.Users
                .AsNoTracking()
                .Where(u => reviewerIdSet.Contains(u.Id))
                .ToDictionaryAsync(u => u.Id, u => u.FirstName + " " + u.LastName);
        
            result.ByReviewer = reviewerRows
                .OrderByDescending(r => r.Total)
                .Select(r => new ReviewerBreakdownRowDto
                {
                    ReviewerId = r.ReviewerId,
                    ReviewerName = reviewerNameDict.TryGetValue(r.ReviewerId, out var rn) ? rn.Trim() : "Unknown",
                    Total = r.Total, Submitted = r.Submitted, UnderReview = r.UnderReview, Pending = r.Pending,
                    Approved = r.Approved, Rejected = r.Rejected, Returned = r.Returned, Completed = r.Completed
                })
                .ToList();
        
            // By month. Each row is a cohort: applications FIRST SUBMITTED in that month, by where they stand today.
            // Created is counted separately, by the month the application was started (drafts included).
            // Months are in Nigerian time (UTC+1). Every month from the first to the last is listed, even with zero activity.
            var monthRows = await BuildFilteredQuery(filter)
                .Where(a => a.SubmittedAt != null)
                .GroupBy(a => new { Year = a.SubmittedAt!.Value.AddHours(1).Year, Month = a.SubmittedAt!.Value.AddHours(1).Month })
                .Select(g => new
                {
                    g.Key.Year,
                    g.Key.Month,
                    Submitted = g.Count(),
                    Awaiting = g.Count(x => x.Status.ToLower() == "submitted" || x.Status.ToLower() == "under_review" || x.Status.ToLower() == "pending"),
                    Approved = g.Count(x => x.Status.ToLower() == "approved" || x.Status.ToLower() == "completed"),
                    Rejected = g.Count(x => x.Status.ToLower() == "rejected"),
                    Returned = g.Count(x => x.Status.ToLower() == "returned" || x.Status.ToLower() == "resubmitted")
                })
                .ToListAsync();

            // Created per month: same filter, but the date window applies to CreatedAt and drafts are included.
            var originalDateField = filter.DateField;
            filter.DateField = "CreatedAt";
            List<(int Year, int Month, int Created)> createdRows;
            try
            {
                var createdRaw = await BuildFilteredQuery(filter, true)
                    .GroupBy(a => new { Year = a.CreatedAt.AddHours(1).Year, Month = a.CreatedAt.AddHours(1).Month })
                    .Select(g => new { g.Key.Year, g.Key.Month, Created = g.Count() })
                    .ToListAsync();
                createdRows = createdRaw.Select(c => (c.Year, c.Month, c.Created)).ToList();
            }
            finally
            {
                filter.DateField = originalDateField;
            }

            var byMonthList = new List<MonthlyTrendRowDto>();
            var monthIndexes = monthRows.Select(m => m.Year * 12 + (m.Month - 1))
                .Concat(createdRows.Select(c => c.Year * 12 + (c.Month - 1)))
                .ToList();
            if (monthIndexes.Count > 0)
            {
                for (var idx = monthIndexes.Min(); idx <= monthIndexes.Max(); idx++)
                {
                    var y = idx / 12;
                    var mo = idx % 12 + 1;
                    var s = monthRows.FirstOrDefault(m => m.Year == y && m.Month == mo);
                    var submitted = s?.Submitted ?? 0;
                    var awaiting = s?.Awaiting ?? 0;
                    var approved = s?.Approved ?? 0;
                    var rejected = s?.Rejected ?? 0;
                    var returned = s?.Returned ?? 0;
                    byMonthList.Add(new MonthlyTrendRowDto
                    {
                        Month = y.ToString("D4") + "-" + mo.ToString("D2"),
                        Created = createdRows.Where(c => c.Year == y && c.Month == mo).Select(c => c.Created).FirstOrDefault(),
                        Submitted = submitted,
                        Awaiting = awaiting,
                        Approved = approved,
                        Rejected = rejected,
                        Returned = returned,
                        Other = Math.Max(0, submitted - awaiting - approved - rejected - returned)
                    });
                }
            }
            result.ByMonth = byMonthList;
        
            // Turnaround: days from submission to the decision
            var decided = await BuildFilteredQuery(filter)
                .Where(a => a.SubmittedAt != null
                    && (a.Status.ToLower() == "approved" || a.Status.ToLower() == "rejected" || a.Status.ToLower() == "returned"))
                .Select(a => new
                {
                    Status = a.Status.ToLower(),
                    a.SubmittedAt,
                    a.CompletedAt,
                    DecisionAt = a.ReviewHistory
                        .Where(h => h.Status.ToLower() == a.Status.ToLower())
                        .Max(h => (DateTime?)h.CreatedAt)
                })
                .ToListAsync();
        
            var days = decided
                .Select(d => new
                {
                    d.Status,
                    Days = ((d.DecisionAt ?? d.CompletedAt) - d.SubmittedAt)?.TotalDays
                })
                .Where(d => d.Days.HasValue && d.Days.Value >= 0)
                .ToList();
        
            double? Avg(IEnumerable<double?> values)
            {
                var list = values.Where(v => v.HasValue).Select(v => v!.Value).ToList();
                return list.Count == 0 ? null : Math.Round(list.Average(), 1);
            }
        
            result.Turnaround = new TurnaroundStatsDto
            {
                DecidedCount = days.Count,
                AverageDays = Avg(days.Select(d => d.Days)),
                ApprovedAverageDays = Avg(days.Where(d => d.Status == "approved").Select(d => d.Days)),
                RejectedAverageDays = Avg(days.Where(d => d.Status == "rejected").Select(d => d.Days)),
                ReturnedAverageDays = Avg(days.Where(d => d.Status == "returned").Select(d => d.Days))
            };
        
            return result;
        }
        
        /// <summary>Rows for the Report information sheet: who, when, filters, counts and scope.</summary>
        private async Task<List<KeyValuePair<string, string>>> BuildExportInfoAsync(
            ReportFilterRequest filter, int totalMatching, int exported, Dictionary<Guid, string> pmiNames, int maxRows)
        {
            var rows = new List<KeyValuePair<string, string>>();
            void Add(string key, string value) => rows.Add(new KeyValuePair<string, string>(key, value));
        
            var nowUtc = DateTime.UtcNow;
            var nowWat = nowUtc.AddHours(1);
        
            var generatedBy = "Not recorded";
            if (filter.RequestedByUserId.HasValue)
            {
                var requesterId = filter.RequestedByUserId.Value;
                var u = await _context.Users.AsNoTracking()
                    .Where(x => x.Id == requesterId)
                    .Select(x => new { x.FirstName, x.LastName, x.Role })
                    .FirstOrDefaultAsync();
                if (u != null)
                    generatedBy = $"{u.FirstName} {u.LastName} ({u.Role})".Trim();
            }
        
            var reviewerText = "All";
            if (filter.ReviewerId.HasValue)
            {
                var reviewerFilterId = filter.ReviewerId.Value;
                var reviewerName = await _context.Users.AsNoTracking()
                    .Where(x => x.Id == reviewerFilterId)
                    .Select(x => x.FirstName + " " + x.LastName)
                    .FirstOrDefaultAsync();
                reviewerText = reviewerName ?? reviewerFilterId.ToString();
            }
        
            var pmiText = "All";
            if (filter.PmiCompanyId.HasValue)
                pmiText = pmiNames.TryGetValue(filter.PmiCompanyId.Value, out var pmiName) ? pmiName : filter.PmiCompanyId.Value.ToString();
        
            Add("Report", "Applications report");
            Add("Generated by", generatedBy);
            Add("Generated (Nigerian time, WAT)", nowWat.ToString("dd MMM yyyy HH:mm"));
            Add("Generated (UTC)", nowUtc.ToString("dd MMM yyyy HH:mm"));
            Add("Rows in this file", exported.ToString("N0"));
            Add("Applications matching the filters", totalMatching.ToString("N0"));
            if (totalMatching > exported)
                Add("Important", $"This file is limited to {maxRows:N0} rows. {(totalMatching - exported):N0} matching applications are not included. Narrow the filters and export again.");
            Add("Date field filtered", filter.DateField);
            Add("From date", filter.DateFrom.HasValue ? filter.DateFrom.Value.ToString("dd MMM yyyy") : "Any");
            Add("To date", filter.DateTo.HasValue ? filter.DateTo.Value.ToString("dd MMM yyyy") : "Any");
            Add("Status", filter.Statuses != null && filter.Statuses.Count > 0 ? string.Join(", ", filter.Statuses) : "All (drafts are never included)");
            Add("Priority", filter.Priorities != null && filter.Priorities.Count > 0 ? string.Join(", ", filter.Priorities) : "All");
            Add("PMI company", pmiText);
            Add("Assigned reviewer", reviewerText);
            Add("Search text", string.IsNullOrWhiteSpace(filter.SearchTerm) ? "None" : filter.SearchTerm!);
            Add("Sorted by", $"{filter.SortBy} ({(filter.SortDescending ? "newest first" : "oldest first")})");
            Add("Scope", "Draft applications and applications that have been stepped down (archived) are not included.");
            Add("Dates and times", "Dates and times in the data sheet are UTC. Date filters use Nigerian (WAT) calendar days.");
            Add("Decision details", "Decision date, turnaround and rejection reason come from the latest review decision that matches the application's current status.");
            return rows;
        }
        
        private static void AddInfoSheet(ExcelPackage package, List<KeyValuePair<string, string>> rows)
        {
            var ws = package.Workbook.Worksheets.Add("Report information");
            ws.Cells[1, 1].Value = "Applications report - information";
            ws.Cells[1, 1].Style.Font.Bold = true;
            ws.Cells[1, 1].Style.Font.Size = 14;
        
            var r = 3;
            foreach (var kv in rows)
            {
                ws.Cells[r, 1].Value = kv.Key;
                ws.Cells[r, 1].Style.Font.Bold = true;
                ws.Cells[r, 2].Value = kv.Value;
                r++;
            }
        
            ws.Column(1).Width = 36;
            ws.Column(2).Width = 95;
            ws.Cells[3, 1, Math.Max(r - 1, 3), 2].Style.WrapText = true;
            ws.Cells[3, 1, Math.Max(r - 1, 3), 2].Style.VerticalAlignment = OfficeOpenXml.Style.ExcelVerticalAlignment.Top;
            package.Workbook.Properties.Title = "Applications report";
            package.Workbook.Properties.Company = "Access Pensions";
        }
        
        private const double ReviewerTargetDays = 2.0; // 48 hours, calendar time

        public async Task<ReviewerActivityReportDto> GetReviewerActivityAsync(DateTime? dateFrom, DateTime? dateTo, bool includeDecisions = true)
        {
            var nowUtc = DateTime.UtcNow;
            // The user's dates are Nigerian (WAT, UTC+1) calendar days; stored times are UTC
            DateTime? fromUtc = dateFrom.HasValue ? dateFrom.Value.Date.AddHours(-1) : (DateTime?)null;
            DateTime? toUtc = dateTo.HasValue ? dateTo.Value.Date.AddDays(1).AddHours(-1) : (DateTime?)null;
        
            // Open work now: active assignments on visible applications that have not been decided
            var openAll = await _context.ApplicationAssignments
                .AsNoTracking()
                .Where(x => x.IsActive)
                .Join(_context.Applications, x => x.ApplicationId, a => a.Id, (x, a) => new { x.ReviewerId, x.AssignedAt, a.Status })
                .ToListAsync();
            var openRows = openAll
                .Where(x =>
                {
                    var s = (x.Status ?? string.Empty).ToLowerInvariant();
                    return s != "approved" && s != "rejected" && s != "completed" && s != "draft" && s != "expired";
                })
                .ToList();
        
            // Decisions made in the period (approved, rejected, returned), on visible applications
            var decisionQuery = _context.Set<Models.ApplicationReviewHistory>()
                .AsNoTracking()
                .Where(h => h.Status.ToLower() == "approved" || h.Status.ToLower() == "rejected" || h.Status.ToLower() == "returned");
            if (fromUtc.HasValue)
            {
                var f = fromUtc.Value;
                decisionQuery = decisionQuery.Where(h => h.CreatedAt >= f);
            }
            if (toUtc.HasValue)
            {
                var t = toUtc.Value;
                decisionQuery = decisionQuery.Where(h => h.CreatedAt < t);
            }
            var decisions = await decisionQuery
                .Where(h => includeDecisions)
                .Join(_context.Applications, h => h.ApplicationId, a => a.Id, (h, a) => new { h.ApplicationId, h.ReviewedById, Status = h.Status.ToLower(), h.CreatedAt })
                .ToListAsync();
        
            // Each reviewer's own assignments for the decided applications (start of their turnaround)
            // Assignments of the decided applications, joined in SQL (no list of thousands of ids sent to the database)
            var assignRows = await decisionQuery
                .Where(h => includeDecisions)
                .Select(h => h.ApplicationId).Distinct()
                .Join(_context.ApplicationAssignments.AsNoTracking(), appId => appId, x => x.ApplicationId, (appId, x) => new { x.ApplicationId, x.ReviewerId, x.AssignedAt })
                .ToListAsync();
            var assignsByApp = assignRows.GroupBy(x => x.ApplicationId).ToDictionary(g => g.Key, g => g.ToList());
        
            // Received in the period
            var receivedQuery = _context.ApplicationAssignments.AsNoTracking().AsQueryable();
            if (fromUtc.HasValue)
            {
                var f2 = fromUtc.Value;
                receivedQuery = receivedQuery.Where(x => x.AssignedAt >= f2);
            }
            if (toUtc.HasValue)
            {
                var t2 = toUtc.Value;
                receivedQuery = receivedQuery.Where(x => x.AssignedAt < t2);
            }
            var receivedRows = await receivedQuery
                .Where(x => includeDecisions)
                .Join(_context.Applications, x => x.ApplicationId, a => a.Id, (x, a) => new { x.ReviewerId })
                .GroupBy(x => x.ReviewerId)
                .Select(g => new { ReviewerId = g.Key, Count = g.Count() })
                .ToListAsync();
            var receivedByReviewer = receivedRows.ToDictionary(r => r.ReviewerId, r => r.Count);
        
            // Who to list: active ADMIN and LEAD_REVIEWER users, plus anyone with work or decisions in scope
            var staff = await _context.Users
                .AsNoTracking()
                .Where(u => u.IsActive && u.Role == "ADMIN")
                .Select(u => u.Id)
                .ToListAsync();
            var reviewerIds = new HashSet<Guid>(staff);
            foreach (var x in openRows) reviewerIds.Add(x.ReviewerId);
            foreach (var d in decisions) reviewerIds.Add(d.ReviewedById);
            foreach (var r in receivedRows) reviewerIds.Add(r.ReviewerId);
        
            var people = await _context.Users
                .AsNoTracking()
                .Where(u => reviewerIds.Contains(u.Id))
                .Select(u => new { u.Id, u.FirstName, u.LastName, u.Email, u.Role })
                .ToDictionaryAsync(u => u.Id);
        
            double? Median(List<double> v)
            {
                if (v.Count == 0) return null;
                var s = v.OrderBy(x => x).ToList();
                var mid = s.Count / 2;
                return s.Count % 2 == 1 ? s[mid] : (s[mid - 1] + s[mid]) / 2.0;
            }
        
            var allHours = new List<double>();
            var rows = new List<ReviewerActivityRowDto>();
        
            foreach (var id in reviewerIds)
            {
                if (!people.TryGetValue(id, out var p) || p.Role != "ADMIN") continue;
        
                var myOpen = openRows.Where(x => x.ReviewerId == id).ToList();
                var myDecisions = decisions.Where(d => d.ReviewedById == id).ToList();
        
                var hours = new List<double>();
                foreach (var d in myDecisions)
                {
                    if (!assignsByApp.TryGetValue(d.ApplicationId, out var list)) continue;
                    var start = list
                        .Where(x => x.ReviewerId == id && x.AssignedAt <= d.CreatedAt)
                        .OrderByDescending(x => x.AssignedAt)
                        .Select(x => (DateTime?)x.AssignedAt)
                        .FirstOrDefault();
                    if (start.HasValue)
                        hours.Add((d.CreatedAt - start.Value).TotalHours);
                }
                allHours.AddRange(hours);
        
                var approved = myDecisions.Count(d => d.Status == "approved");
                var rejected = myDecisions.Count(d => d.Status == "rejected");
                var returned = myDecisions.Count(d => d.Status == "returned");
        
                rows.Add(new ReviewerActivityRowDto
                {
                    ReviewerId = id,
                    ReviewerName = $"{p.FirstName} {p.LastName}".Trim(),
                    Email = p.Email,
                    Role = p.Role,
                    OpenNow = myOpen.Count,
                    OverdueNow = myOpen.Count(x => (nowUtc - x.AssignedAt).TotalDays > ReviewerTargetDays),
                    OldestOpenDays = myOpen.Count == 0 ? null : Math.Round((nowUtc - myOpen.Min(x => x.AssignedAt)).TotalDays, 1),
                    ReceivedInPeriod = receivedByReviewer.TryGetValue(id, out var rc) ? rc : 0,
                    DecisionsInPeriod = myDecisions.Count,
                    Approved = approved,
                    Rejected = rejected,
                    Returned = returned,
                    ReturnedPercent = myDecisions.Count == 0 ? null : Math.Round(returned * 100.0 / myDecisions.Count, 1),
                    TurnaroundSampleSize = hours.Count,
                    AverageTurnaroundDays = hours.Count == 0 ? null : Math.Round(hours.Average() / 24.0, 1),
                    MedianTurnaroundDays = Median(hours.Select(x => x / 24.0).ToList()) is double md ? Math.Round(md, 1) : (double?)null,
                    WithinTargetPercent = hours.Count == 0 ? null : Math.Round(hours.Count(x => x <= ReviewerTargetDays * 24.0) * 100.0 / hours.Count, 1)
                });
            }
        
            rows = rows.OrderBy(r => r.ReviewerName).ToList();
        
            var team = new ReviewerActivityRowDto
            {
                ReviewerName = "All reviewers",
                OpenNow = rows.Sum(r => r.OpenNow),
                OverdueNow = rows.Sum(r => r.OverdueNow),
                OldestOpenDays = rows.Where(r => r.OldestOpenDays.HasValue).Select(r => r.OldestOpenDays).DefaultIfEmpty(null).Max(),
                ReceivedInPeriod = rows.Sum(r => r.ReceivedInPeriod),
                DecisionsInPeriod = rows.Sum(r => r.DecisionsInPeriod),
                Approved = rows.Sum(r => r.Approved),
                Rejected = rows.Sum(r => r.Rejected),
                Returned = rows.Sum(r => r.Returned),
                TurnaroundSampleSize = allHours.Count
            };
            team.ReturnedPercent = team.DecisionsInPeriod == 0 ? null : Math.Round(team.Returned * 100.0 / team.DecisionsInPeriod, 1);
            team.AverageTurnaroundDays = allHours.Count == 0 ? null : Math.Round(allHours.Average() / 24.0, 1);
            team.MedianTurnaroundDays = Median(allHours.Select(x => x / 24.0).ToList()) is double tmd ? Math.Round(tmd, 1) : (double?)null;
            team.WithinTargetPercent = allHours.Count == 0 ? null : Math.Round(allHours.Count(x => x <= ReviewerTargetDays * 24.0) * 100.0 / allHours.Count, 1);
        
            return new ReviewerActivityReportDto
            {
                GeneratedAtUtc = nowUtc,
                PeriodFrom = dateFrom,
                PeriodTo = dateTo,
                TargetDays = ReviewerTargetDays,
                Reviewers = rows,
                Team = team,
                Definitions = new List<string>
                {
                    "Open now: applications currently assigned to the reviewer that have not been approved, rejected or completed.",
                    "Overdue now: open applications assigned more than 2 days (48 hours, calendar time) ago.",
                    "Received: applications assigned to the reviewer during the period.",
                    "Decisions: approvals, rejections and returns recorded by the reviewer during the period. An application can be decided more than once, for example returned and later approved.",
                    "Returned %: returns as a share of the reviewer's decisions in the period.",
                    "Turnaround: time from the reviewer's own assignment to their own decision. Time an application spent with another reviewer is not counted. Decisions without a matching assignment are left out of turnaround.",
                    "Within target: share of decisions made within 2 days of the reviewer's assignment.",
                    "These figures are for oversight and workload planning. They are not ratings or rankings.",
                    "Periods use Nigerian (WAT) calendar days; stored times are UTC. Drafts and stepped-down (archived) applications are not included."
                }
            };
        }
        
        public async Task<byte[]> ExportReviewerActivityToExcelAsync(DateTime? dateFrom, DateTime? dateTo, Guid? requestedByUserId)
        {
            var report = await GetReviewerActivityAsync(dateFrom, dateTo);
        
            var generatedBy = "Not recorded";
            if (requestedByUserId.HasValue)
            {
                var rid = requestedByUserId.Value;
                var u = await _context.Users.AsNoTracking()
                    .Where(x => x.Id == rid)
                    .Select(x => new { x.FirstName, x.LastName, x.Role })
                    .FirstOrDefaultAsync();
                if (u != null) generatedBy = $"{u.FirstName} {u.LastName} ({u.Role})".Trim();
            }
        
            ExcelPackage.LicenseContext = LicenseContext.NonCommercial;
            using (var package = new ExcelPackage())
            {
                var ws = package.Workbook.Worksheets.Add("Reviewer activity");
                var headers = new[]
                {
                    "Reviewer", "Role", "Email", "Open now", "Overdue now", "Oldest open (days)",
                    "Received in period", "Decisions in period", "Approved", "Rejected", "Returned", "Returned %",
                    "Decisions timed", "Average turnaround (days)", "Median turnaround (days)", "Within 2 days %"
                };
                for (int c = 0; c < headers.Length; c++)
                {
                    var cell = ws.Cells[1, c + 1];
                    cell.Value = headers[c];
                    cell.Style.Font.Bold = true;
                    cell.Style.Fill.PatternType = OfficeOpenXml.Style.ExcelFillStyle.Solid;
                    cell.Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.FromArgb(192, 192, 192));
                    cell.Style.HorizontalAlignment = OfficeOpenXml.Style.ExcelHorizontalAlignment.Center;
                    cell.Style.WrapText = true;
                }
        
                var all = report.Reviewers.Concat(new[] { report.Team }).ToList();
                for (int i = 0; i < all.Count; i++)
                {
                    var r = all[i];
                    var row = i + 2;
                    ws.Cells[row, 1].Value = r.ReviewerName;
                    ws.Cells[row, 2].Value = r.Role;
                    ws.Cells[row, 3].Value = r.Email;
                    ws.Cells[row, 4].Value = r.OpenNow;
                    ws.Cells[row, 5].Value = r.OverdueNow;
                    ws.Cells[row, 6].Value = r.OldestOpenDays;
                    ws.Cells[row, 7].Value = r.ReceivedInPeriod;
                    ws.Cells[row, 8].Value = r.DecisionsInPeriod;
                    ws.Cells[row, 9].Value = r.Approved;
                    ws.Cells[row, 10].Value = r.Rejected;
                    ws.Cells[row, 11].Value = r.Returned;
                    ws.Cells[row, 12].Value = r.ReturnedPercent;
                    ws.Cells[row, 13].Value = r.TurnaroundSampleSize;
                    ws.Cells[row, 14].Value = r.AverageTurnaroundDays;
                    ws.Cells[row, 15].Value = r.MedianTurnaroundDays;
                    ws.Cells[row, 16].Value = r.WithinTargetPercent;
                    if (i == all.Count - 1)
                        ws.Cells[row, 1, row, headers.Length].Style.Font.Bold = true;
                }
        
                ws.Row(1).Height = 32;
                ws.View.FreezePanes(2, 2);
                ws.Cells[1, 1, 1, headers.Length].AutoFilter = true;
                ws.Cells.AutoFitColumns();
                for (int c = 1; c <= headers.Length; c++)
                {
                    if (ws.Column(c).Width > 40) ws.Column(c).Width = 40;
                    if (ws.Column(c).Width < 12) ws.Column(c).Width = 12;
                }
                ws.PrinterSettings.Orientation = OfficeOpenXml.eOrientation.Landscape;
                ws.PrinterSettings.FitToPage = true;
                ws.PrinterSettings.FitToWidth = 1;
                ws.PrinterSettings.FitToHeight = 0;
                ws.PrinterSettings.RepeatRows = ws.Cells["1:1"];
        
                var info = new List<KeyValuePair<string, string>>
                {
                    new("Report", "Reviewer activity report"),
                    new("Generated by", generatedBy),
                    new("Generated (Nigerian time, WAT)", report.GeneratedAtUtc.AddHours(1).ToString("dd MMM yyyy HH:mm")),
                    new("Generated (UTC)", report.GeneratedAtUtc.ToString("dd MMM yyyy HH:mm")),
                    new("From date", report.PeriodFrom.HasValue ? report.PeriodFrom.Value.ToString("dd MMM yyyy") : "Any"),
                    new("To date", report.PeriodTo.HasValue ? report.PeriodTo.Value.ToString("dd MMM yyyy") : "Any")
                };
                foreach (var d in report.Definitions)
                {
                    var idx = d.IndexOf(':');
                    info.Add(idx > 0 ? new KeyValuePair<string, string>(d.Substring(0, idx), d.Substring(idx + 1).Trim()) : new KeyValuePair<string, string>("Note", d));
                }
                AddInfoSheet(package, info);
        
                return package.GetAsByteArray();
            }
        }
        
        private IQueryable<Models.Application> BuildFilteredQuery(ReportFilterRequest filter, bool includeDrafts = false)
        {
            var query = _context.Applications
                .AsNoTracking();

            // Always exclude draft applications for reports
            if (!includeDrafts) query = query.Where(a => a.Status.ToLower() != "draft" && a.Status.ToLower() != "expired");

            // Date range filtering
            if (filter.DateFrom.HasValue)
            {
                var startOfDay = filter.DateFrom.Value.Date.AddHours(-1); // start of the Nigerian (WAT) day, in UTC
                switch (filter.DateField)
                {
                    case "CreatedAt":
                        query = query.Where(a => a.CreatedAt >= startOfDay);
                        break;
                    case "SubmittedAt":
                        query = query.Where(a => a.SubmittedAt >= startOfDay);
                        break;
                    case "UpdatedAt":
                        query = query.Where(a => a.UpdatedAt >= startOfDay);
                        break;
                }
            }

            if (filter.DateTo.HasValue)
            {
                var endOfDay = filter.DateTo.Value.Date.AddDays(1).AddHours(-1); // start of the next Nigerian (WAT) day, in UTC
                switch (filter.DateField)
                {
                    case "CreatedAt":
                        query = query.Where(a => a.CreatedAt < endOfDay);
                        break;
                    case "SubmittedAt":
                        query = query.Where(a => a.SubmittedAt < endOfDay);
                        break;
                    case "UpdatedAt":
                        query = query.Where(a => a.UpdatedAt < endOfDay);
                        break;
                }
            }

            // Status filtering - use HashSet to avoid OPENJSON WITH syntax issues
            if (filter.Statuses != null && filter.Statuses.Count > 0)
            {
                var statusList = new HashSet<string>(filter.Statuses.Select(s => s.ToLowerInvariant()));
                query = query.Where(a => statusList.Contains(a.Status.ToLower()));
            }

            // Priority filtering - use HashSet to avoid OPENJSON WITH syntax issues
            if (filter.Priorities != null && filter.Priorities.Count > 0)
            {
                var priorityList = new HashSet<string>(filter.Priorities.Select(p => p.ToLowerInvariant()));
                query = query.Where(a => priorityList.Contains(a.Priority.ToLower()));
            }

            // PMI Company filtering
            if (filter.PmiCompanyId.HasValue)
            {
                query = query.Where(a => a.PmiCompanyId == filter.PmiCompanyId.Value);
            }

            // User filtering
            if (filter.UserId.HasValue)
            {
                query = query.Where(a => a.UserId == filter.UserId.Value);
            }

            // Reviewer filtering (current active assignment)
            if (filter.ReviewerId.HasValue)
            {
                var reviewerFilterId = filter.ReviewerId.Value;
                query = query.Where(a => _context.ApplicationAssignments.Any(x => x.ApplicationId == a.Id && x.IsActive && x.ReviewerId == reviewerFilterId));
            }
            
            // Text search - use simple Contains instead of EF.Functions.Like
            if (!string.IsNullOrWhiteSpace(filter.SearchTerm))
            {
                var searchTerm = filter.SearchTerm.ToLower();
                query = query.Where(a =>
                    a.FirstName.ToLower().Contains(searchTerm) ||
                    a.LastName.ToLower().Contains(searchTerm) ||
                    a.MiddleName.ToLower().Contains(searchTerm) ||
                    a.RsaPin.ToLower().Contains(searchTerm) ||
                    a.ApplicationNumber.ToLower().Contains(searchTerm) ||
                    a.PmiRepName.ToLower().Contains(searchTerm) ||
                    a.PmiCompanyName.ToLower().Contains(searchTerm)
                );
            }

            return query;
        }

        private IQueryable<Models.Application> ApplySorting(
            IQueryable<Models.Application> query,
            string sortBy,
            bool descending)
        {
            query = sortBy switch
            {
                "CreatedAt" => descending
                    ? query.OrderByDescending(a => a.CreatedAt)
                    : query.OrderBy(a => a.CreatedAt),
                "SubmittedAt" => descending
                    ? query.OrderByDescending(a => a.SubmittedAt)
                    : query.OrderBy(a => a.SubmittedAt),
                "UpdatedAt" => descending
                    ? query.OrderByDescending(a => a.UpdatedAt)
                    : query.OrderBy(a => a.UpdatedAt),
                "ApplicationNumber" => descending
                    ? query.OrderByDescending(a => a.ApplicationNumber)
                    : query.OrderBy(a => a.ApplicationNumber),
                "FirstName" => descending
                    ? query.OrderByDescending(a => a.FirstName)
                    : query.OrderBy(a => a.FirstName),
                "LastName" => descending
                    ? query.OrderByDescending(a => a.LastName)
                    : query.OrderBy(a => a.LastName),
                _ => query.OrderByDescending(a => a.CreatedAt)
            };

            // Stable order: ties on the sort field are broken by Id, so pages never repeat or skip rows
            return ((IOrderedQueryable<Models.Application>)query).ThenBy(a => a.Id);
        }

        public async Task<byte[]> ExportApplicationsToExcelAsync(ReportFilterRequest filter)
        {
            filter.Validate();

            // Build the base query
            var query = BuildFilteredQuery(filter);
            query = ApplySorting(query, filter.SortBy, filter.SortDescending);

            // Limit export to 5000 records to prevent memory issues
            const int maxExportRecords = 25000;
            var filtered = BuildFilteredQuery(filter);
            var applications = await query
                .Take(maxExportRecords)
                .Select(a => new { a.Id, a.ApplicationNumber, a.RsaPin, a.FirstName, a.LastName, a.PmiCompanyId, a.PropertyValue, a.LoanAmount, a.Status, a.CreatedAt, a.SubmittedAt })
                .ToListAsync();

            if (applications.Count == 0)
            {
                return GenerateEmptyExcelFile();
            }

            // Load data efficiently using HashSet (sequentially to avoid DbContext concurrency issues)
            

            

            var pmiCompaniesDict = await _context.PMICompanies
                .AsNoTracking()
                .ToDictionaryAsync(p => p.Id, p => p.PmiName);

            var documentCounts = await _context.ApplicationDocuments
                .AsNoTracking()
                .Where(d => filtered.Select(x => x.Id).Contains(d.ApplicationId) && d.IsLatestVersion)
                .GroupBy(d => d.ApplicationId)
                .Select(g => new { ApplicationId = g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.ApplicationId, x => x.Count);

            // Get assigned reviewers (current active assignment)
            var reviewerAssignments = await _context.ApplicationAssignments
                .AsNoTracking()
                .Where(a => filtered.Select(x => x.Id).Contains(a.ApplicationId) && a.IsActive)
                .Select(a => new { a.ApplicationId, a.AssignedAt, a.Reviewer.FirstName, a.Reviewer.LastName, a.Reviewer.Email })
                .ToListAsync();

            var reviewerDict = reviewerAssignments
                .GroupBy(a => a.ApplicationId)
                .ToDictionary(g => g.Key, g => { var latest = g.OrderByDescending(y => y.AssignedAt).First(); return new { latest.FirstName, latest.LastName, latest.Email }; });

            var assignmentDateDict = reviewerAssignments
                .GroupBy(a => a.ApplicationId)
                .ToDictionary(g => g.Key, g => g.OrderByDescending(x => x.AssignedAt).First().AssignedAt);

            // Map to DTOs
            var data = applications.Select(a => new ApplicationReportDto
            {
                ApplicationNumber = a.ApplicationNumber,
                RsaPin = a.RsaPin,
                ApplicantName = $"{a.FirstName} {a.LastName}".Trim(),
                PmiCompany = pmiCompaniesDict.TryGetValue(a.PmiCompanyId, out var pmiName) ? pmiName : string.Empty,
                PropertyValue = a.PropertyValue,
                LoanAmount = a.LoanAmount,
                Status = a.Status,
                CreatedAt = a.CreatedAt,
                SubmittedAt = a.SubmittedAt,
                DocumentCount = documentCounts.TryGetValue(a.Id, out var count) ? count : 0,
                AssignedReviewerName = reviewerDict.TryGetValue(a.Id, out var reviewer) && reviewer != null 
                    ? $"{reviewer.FirstName} {reviewer.LastName}".Trim() 
                    : null,
                AssignedReviewerEmail = reviewerDict.TryGetValue(a.Id, out var reviewer2) && reviewer2 != null 
                    ? reviewer2.Email 
                    : null,
                AssignedAt = assignmentDateDict.TryGetValue(a.Id, out var assignedDate) ? assignedDate : null
            }).ToList();

            await EnrichDecisionAsync(data, filtered.Select(x => x.Id));
            var totalMatching = await filtered.CountAsync();
            var infoRows = await BuildExportInfoAsync(filter, totalMatching, data.Count, pmiCompaniesDict, maxExportRecords);
            // Set EPPlus license context (required for EPPlus 5.0+)
            ExcelPackage.LicenseContext = LicenseContext.NonCommercial;

            using (var package = new ExcelPackage())
            {
                var worksheet = package.Workbook.Worksheets.Add("Applications");

                // Define headers
                var headers = new[] 
                { 
                    "Application Number",
                    "RSA Pin",
                    "Applicant Name",
                    "PMI Company",
                    "Property Value",
                    "Loan Amount",
                    "Status",
                    "Created Date (UTC)",
                    "Submitted Date (UTC)",
                    "Document Count",
                    "Assigned Reviewer Name",
                    "Assigned Reviewer Email",
                    "Assigned Date (UTC)",
                    "Decision Date (UTC)",
                    "Turnaround (days)",
                    "Rejection Reason",
                    "Rejected By",
                    "Rejected Date (UTC)"
                };

                // Add header row
                for (int col = 0; col < headers.Length; col++)
                {
                    var cell = worksheet.Cells[1, col + 1];
                    cell.Value = headers[col];
                    cell.Style.Font.Bold = true;
                    cell.Style.Fill.PatternType = OfficeOpenXml.Style.ExcelFillStyle.Solid;
                    cell.Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.FromArgb(192, 192, 192));
                    cell.Style.HorizontalAlignment = OfficeOpenXml.Style.ExcelHorizontalAlignment.Center;
                }

                // Add data rows
                for (int row = 0; row < data.Count; row++)
                {
                    var app = data[row];
                    var excelRow = row + 2; // Start from row 2 (row 1 is headers)

                    worksheet.Cells[excelRow, 1].Value = app.ApplicationNumber;
                    worksheet.Cells[excelRow, 2].Value = app.RsaPin;
                    worksheet.Cells[excelRow, 3].Value = app.ApplicantName;
                    worksheet.Cells[excelRow, 4].Value = app.PmiCompany;
                    worksheet.Cells[excelRow, 5].Value = app.PropertyValue;
                    worksheet.Cells[excelRow, 6].Value = app.LoanAmount;
                    worksheet.Cells[excelRow, 7].Value = app.Status;
                    worksheet.Cells[excelRow, 8].Value = app.CreatedAt;
                    worksheet.Cells[excelRow, 9].Value = app.SubmittedAt;
                    worksheet.Cells[excelRow, 10].Value = app.DocumentCount;
                    worksheet.Cells[excelRow, 11].Value = app.AssignedReviewerName;
                    worksheet.Cells[excelRow, 12].Value = app.AssignedReviewerEmail;
                    worksheet.Cells[excelRow, 13].Value = app.AssignedAt;
                    worksheet.Cells[excelRow, 14].Value = app.DecisionAt;
                    worksheet.Cells[excelRow, 15].Value = app.TurnaroundDays;
                    worksheet.Cells[excelRow, 16].Value = app.RejectionReason;
                    worksheet.Cells[excelRow, 17].Value = app.RejectedBy;
                    worksheet.Cells[excelRow, 18].Value = app.RejectedAt;
                }

                if (data.Count > 0)
                {
                    var lastDataRow = data.Count + 1;
                    foreach (var moneyCol in new[] { 5, 6 })
                        worksheet.Cells[2, moneyCol, lastDataRow, moneyCol].Style.Numberformat.Format = "\"\u20A6\"#,##0.00";
                    foreach (var dateCol in new[] { 8, 9, 13, 14, 18 })
                        worksheet.Cells[2, dateCol, lastDataRow, dateCol].Style.Numberformat.Format = "yyyy-MM-dd HH:mm:ss";
                }
                // Auto-fit columns
                worksheet.Cells[1, 1, Math.Min(data.Count + 1, 200), headers.Length].AutoFitColumns();
                FormatSheet(worksheet);

                AddInfoSheet(package, infoRows);
                // Return Excel file as byte array
                return package.GetAsByteArray();
            }
        }

        /// <summary>Layout of the report sheet: 18 columns, header in row 1, rejection reason in column 16.</summary>
        private static void FormatSheet(ExcelWorksheet ws)
        {
            const int lastCol = 18;
            const int reasonCol = 16;
            var lastRow = Math.Max(ws.Dimension?.End.Row ?? 1, 1);
        
            // Header row: taller, wrapped, centred
            ws.Row(1).Height = 32;
            var header = ws.Cells[1, 1, 1, lastCol];
            header.Style.WrapText = true;
            header.Style.VerticalAlignment = OfficeOpenXml.Style.ExcelVerticalAlignment.Center;
            header.Style.HorizontalAlignment = OfficeOpenXml.Style.ExcelHorizontalAlignment.Center;
        
            // Keep the header visible while scrolling and add filter buttons
            ws.View.FreezePanes(2, 1);
            header.AutoFilter = true;
        
            // Column widths: cap very wide auto-fit columns; give the reason a fixed wrapped column
            for (int c = 1; c <= lastCol; c++)
            {
                var w = ws.Column(c).Width;
                if (w > 45) ws.Column(c).Width = 45;
                if (w < 12) ws.Column(c).Width = 12;
            }
            ws.Column(reasonCol).Width = 70;
            foreach (var dateCol in new[] { 8, 9, 13, 14, 18 })
                ws.Column(dateCol).Width = 22;
            ws.Column(15).Width = 14;
        
            if (lastRow >= 2)
            {
                var body = ws.Cells[2, 1, lastRow, lastCol];
                body.Style.VerticalAlignment = OfficeOpenXml.Style.ExcelVerticalAlignment.Top;
                ws.Cells[2, reasonCol, lastRow, reasonCol].Style.WrapText = true;
                ws.Cells[2, 15, lastRow, 15].Style.Numberformat.Format = "0.0";
                ws.Cells[2, 15, lastRow, 15].Style.HorizontalAlignment = OfficeOpenXml.Style.ExcelHorizontalAlignment.Center;
            }
        
            // Printing: landscape, fit to one page wide, header repeated on every page
            ws.PrinterSettings.Orientation = OfficeOpenXml.eOrientation.Landscape;
            ws.PrinterSettings.FitToPage = true;
            ws.PrinterSettings.FitToWidth = 1;
            ws.PrinterSettings.FitToHeight = 0;
            ws.PrinterSettings.RepeatRows = ws.Cells["1:1"];
        }
        
        private byte[] GenerateEmptyExcelFile()
        {
            ExcelPackage.LicenseContext = LicenseContext.NonCommercial;

            using (var package = new ExcelPackage())
            {
                var worksheet = package.Workbook.Worksheets.Add("Applications");

                var headers = new[] 
                { 
                    "Application Number",
                    "RSA Pin",
                    "Applicant Name",
                    "PMI Company",
                    "Property Value",
                    "Loan Amount",
                    "Status",
                    "Created Date (UTC)",
                    "Submitted Date (UTC)",
                    "Document Count",
                    "Assigned Reviewer Name",
                    "Assigned Reviewer Email",
                    "Assigned Date (UTC)",
                    "Decision Date (UTC)",
                    "Turnaround (days)",
                    "Rejection Reason",
                    "Rejected By",
                    "Rejected Date (UTC)"
                };

                // Add header row only
                for (int col = 0; col < headers.Length; col++)
                {
                    var cell = worksheet.Cells[1, col + 1];
                    cell.Value = headers[col];
                    cell.Style.Font.Bold = true;
                    cell.Style.Fill.PatternType = OfficeOpenXml.Style.ExcelFillStyle.Solid;
                    cell.Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.FromArgb(192, 192, 192));
                    cell.Style.HorizontalAlignment = OfficeOpenXml.Style.ExcelHorizontalAlignment.Center;
                }

                worksheet.Cells.AutoFitColumns();
                FormatSheet(worksheet);
                return package.GetAsByteArray();
            }
        }
    }

    public static class ExpressionExtensions
    {
        public static string GetMemberName(this System.Linq.Expressions.Expression expression)
        {
            if (expression is System.Linq.Expressions.MemberExpression memberExpression)
            {
                return memberExpression.Member.Name;
            }

            if (expression is System.Linq.Expressions.LambdaExpression lambdaExpression)
            {
                return GetMemberName(lambdaExpression.Body);
            }

            throw new ArgumentException("Expression must be a member or lambda expression");
        }
    }
}
