using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MMGT.API.Data;
using MMGT.API.DTOs.Assignment;
using OfficeOpenXml;

namespace MMGT.API.Controllers
{
    /// <summary>The signed-in reviewer's own queue, with the fields the My Queue screen shows. Read-only. Also exports the filtered queue to Excel.</summary>
    [ApiController]
    [Route("api/assignments/mine")]
    [Authorize(Roles = "REVIEWER,LEAD_REVIEWER,ADMIN")]
    public class MyQueueController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        public MyQueueController(ApplicationDbContext context) { _context = context; }

        private static readonly string[] OpenStatuses = { "submitted", "under_review", "pending", "returned", "resubmitted" };
        private static readonly string[] DecidedStatuses = { "approved", "rejected", "completed" };
        private const int ExportMaxRows = 5000;

        private Guid CurrentUserId()
        {
            var c = User.FindFirst("UserId");
            if (c == null || !Guid.TryParse(c.Value, out var id)) throw new UnauthorizedAccessException("Invalid user token");
            return id;
        }

        private IQueryable<MMGT.API.Models.ApplicationAssignment> Mine(Guid me) =>
            _context.ApplicationAssignments.AsNoTracking()
                .Where(x => x.ReviewerId == me && x.IsActive && x.Application != null && !x.Application.IsArchived);

        /// <summary>Every filter the list and the export understand.</summary>
        private sealed class QueueFilter
        {
            public string Scope = "open";
            public string? Status;
            public string? Priority;
            public string? Search;
            public string Sort = "oldest";
            public int OverdueHours = 48;
            public bool? IsHni;
            public string Focus = string.Empty;
            public DateTime? AssignedFrom;
            public DateTime? AssignedTo;
            public Guid? PmiCompanyId;
            public bool? Resubmitted;
        }

        private static QueueFilter MakeFilter(string? scope, string? status, string? priority, string? search, string? sort,
            int overdueHours, bool? isHni, string? focus, DateTime? assignedFrom, DateTime? assignedTo, Guid? pmiCompanyId, bool? resubmitted)
        {
            var s = (scope ?? "open").Trim().ToLowerInvariant();
            if (s != "open" && s != "decided" && s != "locked") s = "all";
            return new QueueFilter
            {
                Scope = s,
                Status = status,
                Priority = priority,
                Search = search,
                Sort = (sort ?? "oldest").Trim().ToLowerInvariant(),
                OverdueHours = overdueHours < 1 ? 48 : overdueHours,
                IsHni = isHni,
                Focus = (focus ?? string.Empty).Trim().ToLowerInvariant(),
                AssignedFrom = assignedFrom,
                AssignedTo = assignedTo,
                PmiCompanyId = pmiCompanyId,
                Resubmitted = resubmitted
            };
        }

        private IQueryable<MMGT.API.Models.ApplicationAssignment> ApplyFilters(IQueryable<MMGT.API.Models.ApplicationAssignment> q, Guid me, QueueFilter f)
        {
            if (f.Scope == "open") q = q.Where(x => !x.Application.IsLocked && OpenStatuses.Contains(x.Application.Status.ToLower()));
            else if (f.Scope == "decided") q = q.Where(x => DecidedStatuses.Contains(x.Application.Status.ToLower()));
            else if (f.Scope == "locked") q = q.Where(x => x.Application.IsLocked);

            if (!string.IsNullOrWhiteSpace(f.Status))
            {
                var sts = f.Status.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Select(v => v.ToLowerInvariant()).ToArray();
                q = q.Where(x => sts.Contains(x.Application.Status.ToLower()));
            }
            if (!string.IsNullOrWhiteSpace(f.Priority))
            {
                var pr = f.Priority.Trim().ToLowerInvariant();
                q = q.Where(x => x.Application.Priority.ToLower() == pr);
            }
            if (!string.IsNullOrWhiteSpace(f.Search))
            {
                var s = f.Search.Trim().ToLowerInvariant();
                q = q.Where(x => x.Application.ApplicationNumber.ToLower().Contains(s)
                              || x.Application.RsaPin.ToLower().Contains(s)
                              || (x.Application.FirstName + " " + x.Application.LastName).ToLower().Contains(s));
            }

            var focusKey = f.Focus;
            var overdueHours = f.OverdueHours;
            if (focusKey == "overdue")
            {
                var overdueCutoff = DateTime.UtcNow.AddHours(-overdueHours);
                q = q.Where(x => !x.Application.IsLocked && OpenStatuses.Contains(x.Application.Status.ToLower())
                    && ((x.Application.ResubmittedAt != null && x.Application.ResubmittedAt > x.AssignedAt) ? x.Application.ResubmittedAt.Value : x.AssignedAt) < overdueCutoff);
            }
            else if (focusKey == "waitingforclient")
                q = q.Where(x => !x.Application.IsLocked && OpenStatuses.Contains(x.Application.Status.ToLower())
                    && _context.ApplicationDocuments.Any(d => d.ApplicationId == x.ApplicationId && (d.ReviewStatus == "Rejected" || d.ReviewStatus == "RequestedRevision")));
            else if (focusKey == "readytodecide")
                q = q.Where(x => !x.Application.IsLocked && OpenStatuses.Contains(x.Application.Status.ToLower())
                    && !_context.ApplicationDocuments.Any(d => d.ApplicationId == x.ApplicationId && d.ReviewStatus != "Approved"));
            else if (focusKey == "pendingdocuments")
                q = q.Where(x => !x.Application.IsLocked && OpenStatuses.Contains(x.Application.Status.ToLower())
                    && _context.ApplicationDocuments.Any(d => d.ApplicationId == x.ApplicationId && d.ReviewStatus != "Approved" && d.ReviewStatus != "Rejected" && d.ReviewStatus != "RequestedRevision"));
            else if (focusKey == "decidedtoday")
            {
                var dayStartUtc = DateTime.UtcNow.AddHours(1).Date.AddHours(-1);   // Nigeria is UTC+1
                q = q.Where(x => _context.ApplicationReviewHistory.Any(h => h.ApplicationId == x.ApplicationId && h.ReviewedById == me
                    && DecidedStatuses.Contains(h.Status.ToLower()) && h.CreatedAt >= dayStartUtc));
            }
            if (f.IsHni.HasValue) { var wantHni = f.IsHni.Value; q = q.Where(x => x.Application.IsHni == wantHni); }

            // Assigned date range: calendar days in Nigerian time (WAT, UTC+1)
            if (f.AssignedFrom.HasValue) { var from = f.AssignedFrom.Value.Date.AddHours(-1); q = q.Where(x => x.AssignedAt >= from); }
            if (f.AssignedTo.HasValue) { var to = f.AssignedTo.Value.Date.AddDays(1).AddHours(-1); q = q.Where(x => x.AssignedAt < to); }
            if (f.PmiCompanyId.HasValue) { var pid = f.PmiCompanyId.Value; q = q.Where(x => x.Application.PmiCompanyId == pid); }
            if (f.Resubmitted == true) q = q.Where(x => x.Application.ResubmissionCount > 0);
            return q;
        }

        private static IOrderedQueryable<MMGT.API.Models.ApplicationAssignment> Order(IQueryable<MMGT.API.Models.ApplicationAssignment> q, string sort) =>
            sort switch
            {
                "newest" => q.OrderByDescending(x => x.AssignedAt),
                "amount" => q.OrderByDescending(x => x.Application.LoanAmount),
                "waiting" => q.OrderBy(x => (x.Application.ResubmittedAt != null && x.Application.ResubmittedAt > x.AssignedAt) ? x.Application.ResubmittedAt.Value : x.AssignedAt),
                _ => q.OrderBy(x => x.AssignedAt)
            };

        /// <summary>
        /// Turns one page of assignments into the items the screen and the export show.
        /// With scope = null the child lookups use the ids of this page (small). With a scope query (export) they use a subquery, never a long list of ids.
        /// </summary>
        private async Task<List<MyQueueItemDto>> BuildItemsAsync(IQueryable<MMGT.API.Models.ApplicationAssignment> pageQuery, int overdueHours, IQueryable<Guid>? scope)
        {
            var rows = await pageQuery
                .Select(x => new
                {
                    AssignmentId = x.Id, x.AssignedAt, x.AssignedBy, x.ReassignmentReason, x.ApplicationId,
                    Number = x.Application.ApplicationNumber, x.Application.Status, x.Application.Priority,
                    First = x.Application.FirstName, Middle = x.Application.MiddleName, Last = x.Application.LastName,
                    x.Application.RsaPin, PmiName = x.Application.PmiCompany.PmiName,
                    x.Application.LoanAmount, x.Application.PropertyValue, x.Application.CreatedAt, x.Application.SubmittedAt,
                    x.Application.ResubmittedAt, x.Application.ResubmissionCount, x.Application.IsLocked, x.Application.IsHni, x.Application.HniCheckedAt
                }).ToListAsync();

            var appIds = rows.Select(r => r.ApplicationId).ToList();
            var byIds = rows.Where(r => r.AssignedBy.HasValue).Select(r => r.AssignedBy!.Value).Distinct().ToList();

            var assigners = await _context.Users.AsNoTracking().Where(u => byIds.Contains(u.Id))
                .Select(u => new { u.Id, u.FirstName, u.LastName }).ToListAsync();

            var docsQ = _context.ApplicationDocuments.AsNoTracking();
            docsQ = scope != null ? docsQ.Where(d => scope.Contains(d.ApplicationId)) : docsQ.Where(d => appIds.Contains(d.ApplicationId));
            var docCounts = (await docsQ
                .GroupBy(d => new { d.ApplicationId, d.ReviewStatus })
                .Select(g => new { g.Key.ApplicationId, g.Key.ReviewStatus, N = g.Count() }).ToListAsync())
                .GroupBy(d => d.ApplicationId).ToDictionary(g => g.Key, g => g.ToList());

            var eventsQ = _context.ApplicationDocumentEvents.AsNoTracking();
            eventsQ = scope != null ? eventsQ.Where(e => scope.Contains(e.ApplicationId)) : eventsQ.Where(e => appIds.Contains(e.ApplicationId));
            var docEvents = (await eventsQ
                .GroupBy(e => e.ApplicationId).Select(g => new { ApplicationId = g.Key, Last = g.Max(e => e.PerformedAt) }).ToListAsync())
                .ToDictionary(e => e.ApplicationId, e => e.Last);

            var histQ = _context.ApplicationReviewHistory.AsNoTracking();
            histQ = scope != null ? histQ.Where(h => scope.Contains(h.ApplicationId)) : histQ.Where(h => appIds.Contains(h.ApplicationId));
            var hist = await histQ
                .OrderByDescending(h => h.CreatedAt)
                .Select(h => new { h.ApplicationId, h.Status, h.Comments, h.CreatedAt, By = h.ReviewedBy.FirstName + " " + h.ReviewedBy.LastName }).ToListAsync();
            var lastHist = hist.GroupBy(h => h.ApplicationId).ToDictionary(g => g.Key, g => g.First());

            var now = DateTime.UtcNow;
            var items = new List<MyQueueItemDto>();
            foreach (var r in rows)
            {
                docCounts.TryGetValue(r.ApplicationId, out var mineList);
                var mine = mineList ?? new();
                int Sum(Func<string, bool> f) => mine.Where(d => f(d.ReviewStatus ?? string.Empty)).Sum(d => d.N);
                var approved = Sum(s => s == "Approved");
                var rejected = Sum(s => s == "Rejected" || s == "RequestedRevision");
                var total2 = mine.Sum(d => d.N);
                var pending = total2 - approved - rejected;
                var waitingSince = (r.ResubmittedAt.HasValue && r.ResubmittedAt.Value > r.AssignedAt) ? r.ResubmittedAt.Value : r.AssignedAt;
                var hours = Math.Max(0, (now - waitingSince).TotalHours);
                var key = (r.Status ?? string.Empty).ToLowerInvariant();
                var isOpen = !r.IsLocked && OpenStatuses.Contains(key);
                string next = r.IsLocked ? "locked"
                    : !isOpen ? "decided"
                    : pending > 0 ? "reviewDocuments"
                    : rejected > 0 ? "waitingForClient"
                    : "readyToDecide";
                lastHist.TryGetValue(r.ApplicationId, out var lh);
                var who = assigners.FirstOrDefault(a => a.Id == r.AssignedBy);
                DateTime? lastDocActivity = docEvents.TryGetValue(r.ApplicationId, out var ev) ? ev : (DateTime?)null;
                items.Add(new MyQueueItemDto
                {
                    AssignmentId = r.AssignmentId, AssignedAt = r.AssignedAt,
                    AssignedByName = who == null ? "System" : (who.FirstName + " " + who.LastName).Trim(),
                    ReassignmentReason = r.ReassignmentReason,
                    ApplicationId = r.ApplicationId, ApplicationNumber = r.Number, Status = r.Status ?? string.Empty,
                    Priority = r.Priority ?? string.Empty,
                    BorrowerName = string.Join(" ", new[] { r.First, r.Middle, r.Last }.Where(p => !string.IsNullOrWhiteSpace(p))),
                    RsaPin = r.RsaPin, PmiName = r.PmiName, LoanAmount = r.LoanAmount, PropertyValue = r.PropertyValue,
                    CreatedAt = r.CreatedAt, SubmittedAt = r.SubmittedAt, ResubmittedAt = r.ResubmittedAt,
                    ResubmissionCount = r.ResubmissionCount, IsLocked = r.IsLocked, IsHni = r.IsHni, HniCheckedAt = r.HniCheckedAt,
                    WaitingSince = waitingSince, WaitingHours = Math.Round(hours, 1), IsOverdue = isOpen && hours > overdueHours,
                    DocumentsTotal = total2, DocumentsApproved = approved, DocumentsPending = pending, DocumentsRejected = rejected,
                    LastDocumentActivityAt = lastDocActivity,
                    LastActionStatus = lh?.Status, LastActionBy = lh?.By, LastActionAt = lh?.CreatedAt, LastActionComment = lh?.Comments,
                    NextStep = next
                });
            }
            return items;
        }

        [HttpGet("queue")]
        public async Task<ActionResult<MyQueueResponseDto>> Queue(
            [FromQuery] string? scope = "open", [FromQuery] string? status = null, [FromQuery] string? priority = null,
            [FromQuery] string? search = null, [FromQuery] string? sort = "oldest",
            [FromQuery] int page = 1, [FromQuery] int pageSize = 50, [FromQuery] int overdueHours = 48, [FromQuery] bool? isHni = null, [FromQuery] string? focus = null,
            [FromQuery] DateTime? assignedFrom = null, [FromQuery] DateTime? assignedTo = null, [FromQuery] Guid? pmiCompanyId = null, [FromQuery] bool? resubmitted = null)
        {
            var me = CurrentUserId();
            if (page < 1) page = 1;
            if (pageSize < 1) pageSize = 50;
            if (pageSize > 200) pageSize = 200;

            var f = MakeFilter(scope, status, priority, search, sort, overdueHours, isHni, focus, assignedFrom, assignedTo, pmiCompanyId, resubmitted);
            var q = ApplyFilters(Mine(me), me, f);
            var total = await q.CountAsync();
            var ordered = Order(q, f.Sort);

            var items = await BuildItemsAsync(ordered.ThenBy(x => x.Id).Skip((page - 1) * pageSize).Take(pageSize), f.OverdueHours, null);

            return Ok(new MyQueueResponseDto
            {
                Items = items, Total = total, Page = page, PageSize = pageSize, Scope = f.Scope,
                TotalPages = (int)Math.Ceiling(total / (double)pageSize)
            });
        }

        /// <summary>The PMI companies that appear in this reviewer's own queue, for the PMI filter.</summary>
        [HttpGet("pmis")]
        public async Task<IActionResult> Pmis()
        {
            var me = CurrentUserId();
            var rows = await Mine(me)
                .Select(x => new { Id = x.Application.PmiCompanyId, Name = x.Application.PmiCompany.PmiName })
                .Distinct().ToListAsync();
            return Ok(rows.Where(r => !string.IsNullOrWhiteSpace(r.Name)).OrderBy(r => r.Name).Select(r => new { id = r.Id, name = r.Name }));
        }

        /// <summary>
        /// Excel file of the signed-in reviewer's own queue with the same filters as the screen (all pages, up to 5,000 rows).
        /// Only the caller's own assignments can ever be in the file.
        /// </summary>
        [HttpGet("export/excel")]
        public async Task<IActionResult> ExportExcel(
            [FromQuery] string? scope = "open", [FromQuery] string? status = null, [FromQuery] string? priority = null,
            [FromQuery] string? search = null, [FromQuery] string? sort = "oldest", [FromQuery] int overdueHours = 48,
            [FromQuery] bool? isHni = null, [FromQuery] string? focus = null,
            [FromQuery] DateTime? assignedFrom = null, [FromQuery] DateTime? assignedTo = null, [FromQuery] Guid? pmiCompanyId = null, [FromQuery] bool? resubmitted = null)
        {
            try
            {
                var me = CurrentUserId();
                var f = MakeFilter(scope, status, priority, search, sort, overdueHours, isHni, focus, assignedFrom, assignedTo, pmiCompanyId, resubmitted);
                var q = ApplyFilters(Mine(me), me, f);
                var total = await q.CountAsync();

                var items = total == 0
                    ? new List<MyQueueItemDto>()
                    : await BuildItemsAsync(Order(q, f.Sort).ThenBy(x => x.Id).Take(ExportMaxRows), f.OverdueHours, q.Select(x => x.ApplicationId));

                var who = await _context.Users.AsNoTracking().Where(u => u.Id == me)
                    .Select(u => new { u.FirstName, u.LastName, u.Role }).FirstOrDefaultAsync();
                var generatedBy = who == null ? "Not recorded" : $"{who.FirstName} {who.LastName} ({who.Role})".Trim();

                string pmiText = "All";
                if (f.PmiCompanyId.HasValue)
                {
                    var pid = f.PmiCompanyId.Value;
                    pmiText = await _context.PMICompanies.AsNoTracking().Where(p => p.Id == pid).Select(p => p.PmiName).FirstOrDefaultAsync() ?? pid.ToString();
                }

                var bytes = BuildExcel(items, total, f, generatedBy, pmiText);
                var fileName = $"My_Queue_{DateTime.UtcNow:yyyyMMdd_HHmmss}.xlsx";
                return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        private static string NextStepLabel(string key) => key switch
        {
            "reviewDocuments" => "Review documents",
            "waitingForClient" => "Waiting for client",
            "readyToDecide" => "Ready to decide",
            "decided" => "Decided",
            "locked" => "Locked (step-down request)",
            _ => key
        };

        private static string StatusLabel(string s)
        {
            var t = (s ?? string.Empty).Replace('_', ' ');
            return t.Length == 0 ? t : char.ToUpperInvariant(t[0]) + t.Substring(1);
        }

        private static byte[] BuildExcel(List<MyQueueItemDto> items, int total, QueueFilter f, string generatedBy, string pmiText)
        {
            ExcelPackage.LicenseContext = LicenseContext.NonCommercial;
            using (var package = new ExcelPackage())
            {
                var ws = package.Workbook.Worksheets.Add("My queue");
                var headers = new[]
                {
                    "Application Number", "RSA Pin", "Borrower", "PMI Company", "Loan Amount", "Property Value",
                    "Status", "Priority", "HNI", "Locked", "Assigned (UTC)", "Assigned By",
                    "Waiting Since (UTC)", "Waiting (hours)", "Overdue", "First Submitted (UTC)", "Resubmitted (UTC)", "Times Resubmitted",
                    "Documents Total", "Documents Approved", "Documents Pending", "Documents Rejected",
                    "Next Step", "Last Action", "Last Action By", "Last Action (UTC)", "Last Action Comment"
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

                for (int i = 0; i < items.Count; i++)
                {
                    var it = items[i];
                    var r = i + 2;
                    ws.Cells[r, 1].Value = it.ApplicationNumber;
                    ws.Cells[r, 2].Value = it.RsaPin;
                    ws.Cells[r, 3].Value = it.BorrowerName;
                    ws.Cells[r, 4].Value = it.PmiName;
                    ws.Cells[r, 5].Value = it.LoanAmount;
                    ws.Cells[r, 6].Value = it.PropertyValue;
                    ws.Cells[r, 7].Value = StatusLabel(it.Status);
                    ws.Cells[r, 8].Value = StatusLabel(it.Priority);
                    ws.Cells[r, 9].Value = it.IsHni == null ? "Not checked" : (it.IsHni.Value ? "Yes" : "No");
                    ws.Cells[r, 10].Value = it.IsLocked ? "Yes" : "No";
                    ws.Cells[r, 11].Value = it.AssignedAt;
                    ws.Cells[r, 12].Value = it.AssignedByName;
                    ws.Cells[r, 13].Value = it.WaitingSince;
                    ws.Cells[r, 14].Value = it.WaitingHours;
                    ws.Cells[r, 15].Value = it.IsOverdue ? "Yes" : "No";
                    if (it.SubmittedAt.HasValue) ws.Cells[r, 16].Value = it.SubmittedAt.Value;
                    if (it.ResubmittedAt.HasValue) ws.Cells[r, 17].Value = it.ResubmittedAt.Value;
                    ws.Cells[r, 18].Value = it.ResubmissionCount;
                    ws.Cells[r, 19].Value = it.DocumentsTotal;
                    ws.Cells[r, 20].Value = it.DocumentsApproved;
                    ws.Cells[r, 21].Value = it.DocumentsPending;
                    ws.Cells[r, 22].Value = it.DocumentsRejected;
                    ws.Cells[r, 23].Value = NextStepLabel(it.NextStep);
                    ws.Cells[r, 24].Value = string.IsNullOrEmpty(it.LastActionStatus) ? null : StatusLabel(it.LastActionStatus);
                    ws.Cells[r, 25].Value = it.LastActionBy;
                    if (it.LastActionAt.HasValue) ws.Cells[r, 26].Value = it.LastActionAt.Value;
                    ws.Cells[r, 27].Value = it.LastActionComment;
                }

                if (items.Count > 0)
                {
                    var last = items.Count + 1;
                    foreach (var moneyCol in new[] { 5, 6 })
                        ws.Cells[2, moneyCol, last, moneyCol].Style.Numberformat.Format = "\"\u20A6\"#,##0.00";
                    foreach (var dateCol in new[] { 11, 13, 16, 17, 26 })
                        ws.Cells[2, dateCol, last, dateCol].Style.Numberformat.Format = "yyyy-MM-dd HH:mm";
                    ws.Cells[2, 14, last, 14].Style.Numberformat.Format = "0.0";
                    ws.Cells[2, 1, last, headers.Length].Style.VerticalAlignment = OfficeOpenXml.Style.ExcelVerticalAlignment.Top;
                    ws.Cells[2, 27, last, 27].Style.WrapText = true;
                }

                ws.Row(1).Height = 32;
                ws.View.FreezePanes(2, 2);
                ws.Cells[1, 1, 1, headers.Length].AutoFilter = true;
                ws.Cells[1, 1, Math.Min(items.Count + 1, 200), headers.Length].AutoFitColumns();
                for (int c = 1; c <= headers.Length; c++)
                {
                    if (ws.Column(c).Width > 40) ws.Column(c).Width = 40;
                    if (ws.Column(c).Width < 12) ws.Column(c).Width = 12;
                }
                ws.Column(27).Width = 60;
                ws.PrinterSettings.Orientation = OfficeOpenXml.eOrientation.Landscape;
                ws.PrinterSettings.FitToPage = true;
                ws.PrinterSettings.FitToWidth = 1;
                ws.PrinterSettings.FitToHeight = 0;
                ws.PrinterSettings.RepeatRows = ws.Cells["1:1"];

                // Information sheet: who, when, and exactly which filters were applied
                var nowUtc = DateTime.UtcNow;
                var info = new List<KeyValuePair<string, string>>();
                void Add(string k, string v) => info.Add(new KeyValuePair<string, string>(k, v));
                Add("Report", "My queue");
                Add("Generated by", generatedBy);
                Add("Generated (Nigerian time, WAT)", nowUtc.AddHours(1).ToString("dd MMM yyyy HH:mm"));
                Add("Generated (UTC)", nowUtc.ToString("dd MMM yyyy HH:mm"));
                Add("Rows in this file", items.Count.ToString("N0"));
                Add("Applications matching the filters", total.ToString("N0"));
                if (total > items.Count)
                    Add("Important", $"This file is limited to {ExportMaxRows:N0} rows. {(total - items.Count):N0} matching applications are not included. Narrow the filters and export again.");
                Add("Tab", f.Scope == "all" ? "All" : StatusLabel(f.Scope));
                Add("Status", string.IsNullOrWhiteSpace(f.Status) ? "All" : f.Status!.Replace("_", " "));
                Add("Priority", string.IsNullOrWhiteSpace(f.Priority) ? "All" : StatusLabel(f.Priority!));
                Add("HNI", f.IsHni == null ? "All" : (f.IsHni.Value ? "HNI only" : "Not HNI"));
                Add("PMI company", pmiText);
                Add("Assigned from", f.AssignedFrom.HasValue ? f.AssignedFrom.Value.ToString("dd MMM yyyy") : "Any");
                Add("Assigned to", f.AssignedTo.HasValue ? f.AssignedTo.Value.ToString("dd MMM yyyy") : "Any");
                Add("Resubmitted only", f.Resubmitted == true ? "Yes" : "No");
                Add("Quick filter", string.IsNullOrEmpty(f.Focus) ? "None" : f.Focus);
                Add("Search text", string.IsNullOrWhiteSpace(f.Search) ? "None" : f.Search!);
                Add("Sorted by", f.Sort);
                Add("Overdue", $"Open applications waiting more than {f.OverdueHours} hours for you (counted from assignment, or from the client's latest resubmission if later).");
                Add("Dates and times", "Dates and times in the data sheet are UTC. Date filters use Nigerian (WAT) calendar days.");
                Add("Scope", "Only applications currently assigned to you. Stepped-down (archived) applications are not included.");

                var wi = package.Workbook.Worksheets.Add("Report information");
                wi.Cells[1, 1].Value = "My queue - information";
                wi.Cells[1, 1].Style.Font.Bold = true;
                wi.Cells[1, 1].Style.Font.Size = 14;
                var row = 3;
                foreach (var kv in info)
                {
                    wi.Cells[row, 1].Value = kv.Key;
                    wi.Cells[row, 1].Style.Font.Bold = true;
                    wi.Cells[row, 2].Value = kv.Value;
                    row++;
                }
                wi.Column(1).Width = 36;
                wi.Column(2).Width = 95;
                wi.Cells[3, 1, Math.Max(row - 1, 3), 2].Style.WrapText = true;
                wi.Cells[3, 1, Math.Max(row - 1, 3), 2].Style.VerticalAlignment = OfficeOpenXml.Style.ExcelVerticalAlignment.Top;
                package.Workbook.Properties.Title = "My queue";
                package.Workbook.Properties.Company = "Access Pensions";

                return package.GetAsByteArray();
            }
        }

        [HttpGet("summary")]
        public async Task<ActionResult<MyQueueSummaryDto>> Summary([FromQuery] int overdueHours = 48)
        {
            var me = CurrentUserId();
            if (overdueHours < 1) overdueHours = 48;
            var cutoff = DateTime.UtcNow.AddHours(-overdueHours);
            var open = Mine(me).Where(x => !x.Application.IsLocked && OpenStatuses.Contains(x.Application.Status.ToLower()));

            var byStatus = await open.GroupBy(x => x.Application.Status.ToLower()).Select(g => new { Key = g.Key, N = g.Count() }).ToListAsync();
            int N(params string[] keys) => byStatus.Where(b => keys.Contains(b.Key)).Sum(b => b.N);

            var overdue = await open.CountAsync(x =>
                ((x.Application.ResubmittedAt != null && x.Application.ResubmittedAt > x.AssignedAt) ? x.Application.ResubmittedAt.Value : x.AssignedAt) < cutoff);
            var withPending = await open.CountAsync(x => _context.ApplicationDocuments.Any(d => d.ApplicationId == x.ApplicationId && d.ReviewStatus != "Approved" && d.ReviewStatus != "Rejected" && d.ReviewStatus != "RequestedRevision"));
            var waiting = await open.CountAsync(x => _context.ApplicationDocuments.Any(d => d.ApplicationId == x.ApplicationId && (d.ReviewStatus == "Rejected" || d.ReviewStatus == "RequestedRevision")));
            var ready = await open.CountAsync(x => !_context.ApplicationDocuments.Any(d => d.ApplicationId == x.ApplicationId && d.ReviewStatus != "Approved"));
            var locked = await Mine(me).CountAsync(x => x.Application.IsLocked);

            var hniOpen = await open.CountAsync(x => x.Application.IsHni == true);
            var todayStartUtc = DateTime.UtcNow.AddHours(1).Date.AddHours(-1);   // Nigeria is UTC+1
            var weekStartUtc = DateTime.UtcNow.AddDays(-7);
            var decided = _context.ApplicationReviewHistory.AsNoTracking()
                .Where(h => h.ReviewedById == me && DecidedStatuses.Contains(h.Status.ToLower()));
            var today = await decided.CountAsync(h => h.CreatedAt >= todayStartUtc);
            var week = await decided.CountAsync(h => h.CreatedAt >= weekStartUtc);

            return Ok(new MyQueueSummaryDto
            {
                Open = byStatus.Sum(b => b.N), Submitted = N("submitted"), UnderReview = N("under_review"), Pending = N("pending"),
                ReturnedByClient = N("returned", "resubmitted"), Overdue = overdue, WithPendingDocuments = withPending,
                WaitingForClient = waiting, ReadyToDecide = ready, Locked = locked, HniOpen = hniOpen,
                DecidedToday = today, DecidedLast7Days = week, OverdueHours = overdueHours
            });
        }
    }
}
