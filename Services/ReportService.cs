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
                .Where(d => documentCountsHashSet.Contains(d.ApplicationId))
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
                .ToDictionary(g => g.Key, g => g.First().Reviewer);

            var assignmentDateDict = reviewerAssignments
                .GroupBy(a => a.ApplicationId)
                .ToDictionary(g => g.Key, g => g.First().AssignedAt);

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
                .Where(d => documentCountsHashSet.Contains(d.ApplicationId))
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
                .ToDictionary(g => g.Key, g => g.First().Reviewer);

            var assignmentDateDict = reviewerAssignments
                .GroupBy(a => a.ApplicationId)
                .ToDictionary(g => g.Key, g => g.First().AssignedAt);

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

        private IQueryable<Models.Application> BuildFilteredQuery(ReportFilterRequest filter)
        {
            var query = _context.Applications
                .AsNoTracking();

            // Always exclude draft applications for reports
            query = query.Where(a => a.Status.ToLower() != "draft");

            // Date range filtering
            if (filter.DateFrom.HasValue)
            {
                var startOfDay = filter.DateFrom.Value.Date;
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
                var endOfDay = filter.DateTo.Value.Date.AddDays(1);
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

            return query;
        }

        public async Task<byte[]> ExportApplicationsToExcelAsync(ReportFilterRequest filter)
        {
            filter.Validate();

            // Build the base query
            var query = BuildFilteredQuery(filter);
            query = ApplySorting(query, filter.SortBy, filter.SortDescending);

            // Limit export to 5000 records to prevent memory issues
            const int maxExportRecords = 5000;
            var appIds = await query
                .Select(a => a.Id)
                .Take(maxExportRecords)
                .ToListAsync();

            if (appIds.Count == 0)
            {
                return GenerateEmptyExcelFile();
            }

            // Load data efficiently using HashSet (sequentially to avoid DbContext concurrency issues)
            var appIdHashSet = new HashSet<Guid>(appIds);

            var applications = await _context.Applications
                .AsNoTracking()
                .Where(a => appIdHashSet.Contains(a.Id))
                .ToListAsync();

            var pmiCompaniesDict = await _context.PMICompanies
                .AsNoTracking()
                .ToDictionaryAsync(p => p.Id, p => p.PmiName);

            var documentCounts = await _context.ApplicationDocuments
                .AsNoTracking()
                .Where(d => appIdHashSet.Contains(d.ApplicationId))
                .GroupBy(d => d.ApplicationId)
                .Select(g => new { ApplicationId = g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.ApplicationId, x => x.Count);

            // Get assigned reviewers (current active assignment)
            var reviewerAssignments = await _context.ApplicationAssignments
                .AsNoTracking()
                .Where(a => appIdHashSet.Contains(a.ApplicationId) && a.IsActive)
                .Include(a => a.Reviewer)
                .ToListAsync();

            var reviewerDict = reviewerAssignments
                .GroupBy(a => a.ApplicationId)
                .ToDictionary(g => g.Key, g => g.First().Reviewer);

            var assignmentDateDict = reviewerAssignments
                .GroupBy(a => a.ApplicationId)
                .ToDictionary(g => g.Key, g => g.First().AssignedAt);

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
                    "Created Date",
                    "Submitted Date",
                    "Document Count",
                    "Assigned Reviewer Name",
                    "Assigned Reviewer Email",
                    "Assigned Date"
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
                    worksheet.Cells[excelRow, 5].Style.Numberformat.Format = "$#,##0.00";
                    worksheet.Cells[excelRow, 6].Value = app.LoanAmount;
                    worksheet.Cells[excelRow, 6].Style.Numberformat.Format = "$#,##0.00";
                    worksheet.Cells[excelRow, 7].Value = app.Status;
                    worksheet.Cells[excelRow, 8].Value = app.CreatedAt;
                    worksheet.Cells[excelRow, 8].Style.Numberformat.Format = "yyyy-MM-dd HH:mm:ss";
                    worksheet.Cells[excelRow, 9].Value = app.SubmittedAt;
                    worksheet.Cells[excelRow, 9].Style.Numberformat.Format = "yyyy-MM-dd HH:mm:ss";
                    worksheet.Cells[excelRow, 10].Value = app.DocumentCount;
                    worksheet.Cells[excelRow, 11].Value = app.AssignedReviewerName;
                    worksheet.Cells[excelRow, 12].Value = app.AssignedReviewerEmail;
                    worksheet.Cells[excelRow, 13].Value = app.AssignedAt;
                    worksheet.Cells[excelRow, 13].Style.Numberformat.Format = "yyyy-MM-dd HH:mm:ss";
                }

                // Auto-fit columns
                worksheet.Cells.AutoFitColumns();

                // Return Excel file as byte array
                return package.GetAsByteArray();
            }
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
                    "Created Date",
                    "Submitted Date",
                    "Document Count",
                    "Assigned Reviewer Name",
                    "Assigned Reviewer Email",
                    "Assigned Date"
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
