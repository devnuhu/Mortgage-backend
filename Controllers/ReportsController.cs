using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using MMGT.API.DTOs.Application;
using MMGT.API.DTOs.Report;
using MMGT.API.Services.Interfaces;

namespace MMGT.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class ReportsController : ControllerBase
    {
        private readonly IReportService _reportService;

        public ReportsController(IReportService reportService)
        {
            _reportService = reportService;
        }

        /// <summary>
        /// Generate a filtered application report with essential fields only.
        /// Returns: Application Number, RSA Pin, Applicant Name, PMI Company, Property Value, 
        /// Loan Amount, Status, Date Created, Date Submitted, Document Upload Count.
        /// Returns paginated results (default 100 items per page).
        /// Each subsequent page query hits the database fresh to ensure current data.
        /// </summary>
        /// <param name="pageNumber">Page number (default: 1)</param>
        /// <param name="pageSize">Items per page (default: 100, max: 500)</param>
        /// <param name="dateFrom">Filter start date (inclusive)</param>
        /// <param name="dateTo">Filter end date (inclusive)</param>
        /// <param name="dateField">Which date field to filter on: CreatedAt, SubmittedAt, UpdatedAt (default: CreatedAt)</param>
        /// <param name="statuses">Comma-separated status values: submitted, under_review, approved, rejected, returned, completed</param>
        /// <param name="priorities">Comma-separated priority values: low, medium, high, urgent</param>
        /// <param name="pmiCompanyId">Filter by PMI Company ID</param>
        /// <param name="userId">Filter by User ID</param>
        /// <param name="searchTerm">Text search across name, RSA pin, application number, PMI rep name, company name</param>
        /// <param name="sortBy">Sort field: CreatedAt, SubmittedAt, UpdatedAt, ApplicationNumber, FirstName, LastName (default: CreatedAt)</param>
        /// <param name="sortDescending">Sort direction (default: true for descending)</param>
        /// <returns>Paginated report with essential fields and summary statistics</returns>
        [HttpGet("applications")]
        [Authorize(Roles = "SUPER_ADMIN,ADMIN,LEAD_REVIEWER")]
        public async Task<ActionResult<ReportResponse<ApplicationReportDto>>> GetApplicationReport(
            [FromQuery] int pageNumber = 1,
            [FromQuery] int pageSize = 100,
            [FromQuery] DateTime? dateFrom = null,
            [FromQuery] DateTime? dateTo = null,
            [FromQuery] string dateField = "CreatedAt",
            [FromQuery] string? statuses = null,
            [FromQuery] string? priorities = null,
            [FromQuery] Guid? pmiCompanyId = null,
            [FromQuery] Guid? userId = null,
            [FromQuery] string? searchTerm = null,
            [FromQuery] string sortBy = "CreatedAt",
            [FromQuery] bool sortDescending = true)
        {
            try
            {
                var filter = new ReportFilterRequest
                {
                    PageNumber = pageNumber,
                    PageSize = pageSize,
                    DateFrom = dateFrom,
                    DateTo = dateTo,
                    DateField = dateField,
                    Statuses = ParseCommaSeparatedList(statuses),
                    Priorities = ParseCommaSeparatedList(priorities),
                    PmiCompanyId = pmiCompanyId,
                    UserId = userId,
                    SearchTerm = searchTerm,
                    SortBy = sortBy,
                    SortDescending = sortDescending
                };

                var result = await _reportService.GenerateApplicationReportAsync(filter);
                return Ok(result);
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>
        /// Generate a complete filtered application report with all fields.
        /// Returns full ApplicationDto with all details including ID, user info, PMI rep details, etc.
        /// Returns paginated results (default 100 items per page).
        /// Each subsequent page query hits the database fresh to ensure current data.
        /// </summary>
        /// <param name="pageNumber">Page number (default: 1)</param>
        /// <param name="pageSize">Items per page (default: 100, max: 500)</param>
        /// <param name="dateFrom">Filter start date (inclusive)</param>
        /// <param name="dateTo">Filter end date (inclusive)</param>
        /// <param name="dateField">Which date field to filter on: CreatedAt, SubmittedAt, UpdatedAt (default: CreatedAt)</param>
        /// <param name="statuses">Comma-separated status values: submitted, under_review, approved, rejected, returned, completed</param>
        /// <param name="priorities">Comma-separated priority values: low, medium, high, urgent</param>
        /// <param name="pmiCompanyId">Filter by PMI Company ID</param>
        /// <param name="userId">Filter by User ID</param>
        /// <param name="searchTerm">Text search across name, RSA pin, application number, PMI rep name, company name</param>
        /// <param name="sortBy">Sort field: CreatedAt, SubmittedAt, UpdatedAt, ApplicationNumber, FirstName, LastName (default: CreatedAt)</param>
        /// <param name="sortDescending">Sort direction (default: true for descending)</param>
        /// <returns>Paginated full report with all application details and summary statistics</returns>
        [HttpGet("applications/full")]
        [Authorize(Roles = "SUPER_ADMIN,ADMIN,LEAD_REVIEWER")]
        public async Task<ActionResult<ReportResponse<ApplicationDto>>> GetApplicationReportFull(
            [FromQuery] int pageNumber = 1,
            [FromQuery] int pageSize = 100,
            [FromQuery] DateTime? dateFrom = null,
            [FromQuery] DateTime? dateTo = null,
            [FromQuery] string dateField = "CreatedAt",
            [FromQuery] string? statuses = null,
            [FromQuery] string? priorities = null,
            [FromQuery] Guid? pmiCompanyId = null,
            [FromQuery] Guid? userId = null,
            [FromQuery] string? searchTerm = null,
            [FromQuery] string sortBy = "CreatedAt",
            [FromQuery] bool sortDescending = true)
        {
            try
            {
                var filter = new ReportFilterRequest
                {
                    PageNumber = pageNumber,
                    PageSize = pageSize,
                    DateFrom = dateFrom,
                    DateTo = dateTo,
                    DateField = dateField,
                    Statuses = ParseCommaSeparatedList(statuses),
                    Priorities = ParseCommaSeparatedList(priorities),
                    PmiCompanyId = pmiCompanyId,
                    UserId = userId,
                    SearchTerm = searchTerm,
                    SortBy = sortBy,
                    SortDescending = sortDescending
                };

                var result = await _reportService.GenerateApplicationReportFullAsync(filter);
                return Ok(result);
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>
        /// Get summary statistics for applications matching the filter criteria.
        /// Includes counts by status and aggregate financial data.
        /// </summary>
        [HttpGet("applications/summary")]
        [Authorize(Roles = "SUPER_ADMIN,ADMIN,LEAD_REVIEWER")]
        public async Task<ActionResult<ReportSummaryDto>> GetApplicationSummary(
            [FromQuery] DateTime? dateFrom = null,
            [FromQuery] DateTime? dateTo = null,
            [FromQuery] string dateField = "CreatedAt",
            [FromQuery] string? statuses = null,
            [FromQuery] string? priorities = null,
            [FromQuery] Guid? pmiCompanyId = null,
            [FromQuery] Guid? userId = null,
            [FromQuery] string? searchTerm = null)
        {
            try
            {
                var filter = new ReportFilterRequest
                {
                    DateFrom = dateFrom,
                    DateTo = dateTo,
                    DateField = dateField,
                    Statuses = ParseCommaSeparatedList(statuses),
                    Priorities = ParseCommaSeparatedList(priorities),
                    PmiCompanyId = pmiCompanyId,
                    UserId = userId,
                    SearchTerm = searchTerm
                };

                var summary = await _reportService.GetReportSummaryAsync(filter);
                return Ok(summary);
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>
        /// Export all filtered application records to Excel.
        /// Returns all matching records (respects filters, ignores pagination).
        /// Useful for exporting reports with many records (e.g., 1000+ records).
        /// </summary>
        /// <param name="dateFrom">Filter start date (inclusive)</param>
        /// <param name="dateTo">Filter end date (inclusive)</param>
        /// <param name="dateField">Which date field to filter on: CreatedAt, SubmittedAt, UpdatedAt (default: CreatedAt)</param>
        /// <param name="statuses">Comma-separated status values: submitted, under_review, approved, rejected, returned, completed</param>
        /// <param name="priorities">Comma-separated priority values: low, medium, high, urgent</param>
        /// <param name="pmiCompanyId">Filter by PMI Company ID</param>
        /// <param name="userId">Filter by User ID</param>
        /// <param name="searchTerm">Text search across name, RSA pin, application number, PMI rep name, company name</param>
        /// <param name="sortBy">Sort field: CreatedAt, SubmittedAt, UpdatedAt, ApplicationNumber, FirstName, LastName (default: CreatedAt)</param>
        /// <param name="sortDescending">Sort direction (default: true for descending)</param>
        /// <returns>Excel file containing all filtered application records</returns>
        [HttpGet("applications/export/excel")]
        [Authorize(Roles = "SUPER_ADMIN,ADMIN,LEAD_REVIEWER")]
        public async Task<IActionResult> ExportApplicationsToExcel(
            [FromQuery] DateTime? dateFrom = null,
            [FromQuery] DateTime? dateTo = null,
            [FromQuery] string dateField = "CreatedAt",
            [FromQuery] string? statuses = null,
            [FromQuery] string? priorities = null,
            [FromQuery] Guid? pmiCompanyId = null,
            [FromQuery] Guid? userId = null,
            [FromQuery] string? searchTerm = null,
            [FromQuery] string sortBy = "CreatedAt",
            [FromQuery] bool sortDescending = true)
        {
            try
            {
                var filter = new ReportFilterRequest
                {
                    DateFrom = dateFrom,
                    DateTo = dateTo,
                    DateField = dateField,
                    Statuses = ParseCommaSeparatedList(statuses),
                    Priorities = ParseCommaSeparatedList(priorities),
                    PmiCompanyId = pmiCompanyId,
                    UserId = userId,
                    SearchTerm = searchTerm,
                    SortBy = sortBy,
                    SortDescending = sortDescending
                };

                var excelData = await _reportService.ExportApplicationsToExcelAsync(filter);

                // Generate filename with timestamp
                var fileName = $"Applications_Export_{DateTime.UtcNow:yyyyMMdd_HHmmss}.xlsx";

                return File(excelData, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        private List<string>? ParseCommaSeparatedList(string? input)
        {
            if (string.IsNullOrWhiteSpace(input))
                return null;

            return input
                .Split(',')
                .Select(s => s.Trim())
                .Where(s => !string.IsNullOrEmpty(s))
                .ToList();
        }
    }
}
