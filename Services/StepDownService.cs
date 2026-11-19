using Microsoft.EntityFrameworkCore;
using MMGT.API.Data;
using MMGT.API.DTOs.StepDown;
using MMGT.API.Models;

namespace MMGT.API.Services
{
    public interface IStepDownService
    {
        Task<StepDownRequestDto> CreateAsync(Guid applicationId, string reason, StepDownActor actor);
        Task<StepDownRequestDto> SubmitAsync(Guid requestId, StepDownActor actor);
        Task<StepDownRequestDto> CancelDraftAsync(Guid requestId, string? reason, StepDownActor actor);
        Task<StepDownRequestDto> RemoveDraftAsync(Guid requestId, StepDownActor actor);
        Task<StepDownRequestDto> HodApproveAsync(Guid requestId, string? comment, StepDownActor actor);
        Task<StepDownRequestDto> HodRejectAsync(Guid requestId, string comment, StepDownActor actor);
        Task<StepDownRequestDto> StepDownAsync(Guid requestId, string? comment, StepDownActor actor);
        Task<StepDownRequestDto> CheckerRejectAsync(Guid requestId, string comment, StepDownActor actor);
        Task<StepDownRequestDto> RequestCancellationAsync(Guid requestId, string reason, StepDownActor actor);
        Task<StepDownRequestDto> DecideCancellationAsync(Guid requestId, bool accept, string? comment, StepDownActor actor);
        Task<StepDownRequestDto> RestoreAsync(Guid requestId, string reason, StepDownActor actor);
        Task<StepDownRequestDto?> GetAsync(Guid requestId);
        Task<List<StepDownRequestDto>> ListAsync(string? scope, string? status, StepDownActor actor, int page, int pageSize);
        Task<List<StepDownRequestDto>> ListForApplicationAsync(Guid applicationId);
        Task<Dictionary<string, int>> GetSummaryAsync();
        Task<List<StepDownAuditDto>> GetAuditAsync(Guid requestId);
        Task<StepDownRequestDto> SaveLetterAsync(Guid requestId, string? fileName, byte[] content, StepDownActor actor);
        Task<StepDownLetterFile?> GetLetterAsync(Guid requestId);
    }

    /// <summary>
    /// Maker (SERVICE_MANAGER) raises a step-down request, the Head of Benefit Administration approves it,
    /// then the Checker (MORTGAGE_LEAD) reviews and steps the application down (soft delete, recoverable) or rejects it.
    /// Nothing is ever deleted: a stepped-down application is only flagged IsArchived and hidden from normal queries.
    /// Every action writes an append-only audit row in the same save as the change itself.
    /// </summary>
    public class StepDownService : IStepDownService
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<StepDownService> _logger;
        private readonly string[] _makerRoles;
        private readonly IStepDownNotifier _notifier;

        public StepDownService(ApplicationDbContext context, ILogger<StepDownService> logger, IConfiguration configuration, IStepDownNotifier notifier)
        {
            _context = context;
            _logger = logger;
            _notifier = notifier;
            // Who may raise step-down requests. Setting: StepDown:MakerRoles (comma separated). Default: LEAD_REVIEWER.
            var configured = configuration["StepDown:MakerRoles"];
            _makerRoles = (string.IsNullOrWhiteSpace(configured) ? "LEAD_REVIEWER" : configured)
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        }

        private bool IsMaker(StepDownActor a) =>
            _makerRoles.Any(r => string.Equals(r, a.Role, StringComparison.OrdinalIgnoreCase));

        private void RequireMaker(StepDownActor a, string what)
        {
            if (!IsMaker(a))
                throw new InvalidOperationException($"Your role is not allowed to {what}. Allowed: {string.Join(", ", _makerRoles)}.");
        }
        // ---------- small helpers ----------
        private static bool Is(StepDownActor a, string role) =>
            string.Equals(a.Role, role, StringComparison.OrdinalIgnoreCase);

        private static void Require(StepDownActor a, string role, string what)
        {
            if (!Is(a, role)) throw new InvalidOperationException($"Only {role} can {what}.");
        }

        private static void Expect(ApplicationStepDownRequest r, params string[] statuses)
        {
            if (!statuses.Contains(r.Status))
                throw new InvalidOperationException($"This request is '{r.Status}', so this action is not available.");
        }

        private static string? Clip(string? s, int max)
        {
            if (string.IsNullOrWhiteSpace(s)) return null;
            s = s.Trim();
            return s.Length <= max ? s : s.Substring(0, max);
        }

        private static string RequiredText(string? s, string what)
        {
            var t = Clip(s, 1000);
            if (t == null) throw new InvalidOperationException($"{what} is required.");
            return t;
        }

        private static void NotOwnRequest(ApplicationStepDownRequest r, StepDownActor a, string what)
        {
            if (r.RequestedById == a.UserId)
                throw new InvalidOperationException($"You cannot {what} a request you raised yourself.");
        }

        private static void OnlyRequester(ApplicationStepDownRequest r, StepDownActor a)
        {
            if (r.RequestedById != a.UserId)
                throw new InvalidOperationException("Only the person who raised this request can do this.");
        }

        private async Task<ApplicationStepDownRequest> LoadAsync(Guid id)
        {
            var r = await _context.ApplicationStepDownRequests.FirstOrDefaultAsync(x => x.Id == id);
            if (r == null) throw new InvalidOperationException("Step-down request not found.");
            return r;
        }

        // Looks past the "hide archived" filter: a request must still reach its application after step-down.
        private async Task<Application> LoadApplicationAsync(Guid id)
        {
            var a = await _context.Applications.IgnoreQueryFilters().FirstOrDefaultAsync(x => x.Id == id);
            if (a == null) throw new InvalidOperationException("Application not found.");
            return a;
        }

        private async Task<string> NameOfAsync(Guid userId)
        {
            var u = await _context.Users.AsNoTracking().Where(x => x.Id == userId)
                .Select(x => new { x.FirstName, x.LastName }).FirstOrDefaultAsync();
            return u == null ? "Unknown user" : $"{u.FirstName} {u.LastName}".Trim();
        }

        private async Task AuditAsync(ApplicationStepDownRequest r, string action, string? from, string? to,
            StepDownActor actor, string? comment = null, string? details = null)
        {
            _context.ApplicationStepDownAudits.Add(new ApplicationStepDownAudit
            {
                RequestId = r.Id,
                ApplicationId = r.ApplicationId,
                Action = action,
                FromStatus = from,
                ToStatus = to,
                PerformedById = actor.UserId,
                PerformedByName = await NameOfAsync(actor.UserId),
                PerformedByRole = actor.Role,
                PerformedAt = DateTime.UtcNow,
                Comment = Clip(comment, 1000),
                Details = Clip(details, 500)
            });
        }

        private async Task SaveAsync()
        {
            try { await _context.SaveChangesAsync(); }
            catch (DbUpdateConcurrencyException)
            {
                throw new InvalidOperationException("This request was changed by someone else a moment ago. Refresh and try again.");
            }
        }

        // Emails and the borrower SMS (step M4). A failure here never undoes the action; it is written to the audit trail.
        private async Task NotifyAsync(string action, ApplicationStepDownRequest r)
        {
            _logger.LogInformation("[STEPDOWN] {Action} on request {RequestId} for application {ApplicationId}", action, r.Id, r.ApplicationId);
            try
            {
                var res = await _notifier.NotifyAsync(action, r);
                var who = r.ActionedById ?? r.DecidedById ?? r.RequestedById;
                void Note(string act, string details) => _context.ApplicationStepDownAudits.Add(new ApplicationStepDownAudit
                {
                    RequestId = r.Id, ApplicationId = r.ApplicationId, Action = act,
                    FromStatus = r.Status, ToStatus = r.Status,
                    PerformedById = who, PerformedByName = "System", PerformedByRole = "SYSTEM",
                    PerformedAt = DateTime.UtcNow, Details = Clip(details, 500)
                });
                foreach (var f in res.EmailFailures) Note(StepDownAction.EmailNotSent, f);
                if (res.SmsOutcome != null)
                    Note(res.SmsOutcome == "queued" ? StepDownAction.SmsQueued : StepDownAction.SmsNotSent, "Borrower SMS " + res.SmsOutcome);
                if (res.EmailFailures.Count > 0 || res.SmsOutcome != null) await _context.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[STEPDOWN] Notification step failed for request {RequestId}; the action itself was saved.", r.Id);
            }
        }

        // ---------- approval route (by RSA PIN) and signed request letter ----------
        // Counts the earlier step-downs of the same RSA PIN (any application), newest first. A rejection breaks the row.
        // Cancelled and removed drafts are ignored. A restored request does NOT count (the application is live again) and does not break the row.
        // No RSA PIN on file: the stricter route (HOD) is used because nothing can be counted.
        private async Task<(bool RequiresHod, int Run)> DecideRouteAsync(Guid requestId, Guid applicationId)
        {
            var pinRaw = await _context.Applications.IgnoreQueryFilters().AsNoTracking()
                .Where(a => a.Id == applicationId).Select(a => a.RsaPin).FirstOrDefaultAsync();
            var pin = (pinRaw ?? string.Empty).Trim().ToUpperInvariant();
            if (pin.Length == 0) return (true, 0);
        
            var rows = await (from q in _context.ApplicationStepDownRequests.AsNoTracking()
                              join a in _context.Applications.IgnoreQueryFilters().AsNoTracking() on q.ApplicationId equals a.Id
                              where q.Id != requestId && a.RsaPin != null && a.RsaPin.Trim().ToUpper() == pin
                                    && (q.Status == StepDownStatus.SteppedDown || q.Status == StepDownStatus.Rejected)
                              select new { q.Status, q.ActionedAt, q.DecidedAt, q.SubmittedAt, q.CreatedAt }).ToListAsync();
        
            var run = 0;
            foreach (var x in rows.OrderByDescending(x => x.ActionedAt ?? x.DecidedAt ?? x.SubmittedAt ?? x.CreatedAt))
            {
                if (x.Status == StepDownStatus.Rejected) break;
                run++;
            }
            return (run >= StepDownRouting.HodAfterInARow, run);
        }
        
        // The checker may act when the HOD has approved, or straight away on the checker-only route.
        private static void ExpectWithChecker(ApplicationStepDownRequest r)
        {
            if (r.Status == StepDownStatus.Approved) return;
            if (r.Status == StepDownStatus.Submitted && !r.RequiresHodApproval) return;
            if (r.Status == StepDownStatus.Submitted)
                throw new InvalidOperationException("This request is waiting for the HOD Benefit Admin to approve it first.");
            throw new InvalidOperationException($"This request is '{r.Status}', so this action is not available.");
        }
        
        // Name and content must agree: .pdf must start with %PDF-, .jpg/.jpeg with FF D8 FF, .png with the PNG signature.
        private static string? LetterContentType(string ext, byte[] c)
        {
            if (ext == ".pdf" && c.Length >= 5 && c[0] == 0x25 && c[1] == 0x50 && c[2] == 0x44 && c[3] == 0x46 && c[4] == 0x2D) return "application/pdf";
            if ((ext == ".jpg" || ext == ".jpeg") && c.Length >= 3 && c[0] == 0xFF && c[1] == 0xD8 && c[2] == 0xFF) return "image/jpeg";
            if (ext == ".png" && c.Length >= 8 && c[0] == 0x89 && c[1] == 0x50 && c[2] == 0x4E && c[3] == 0x47 && c[4] == 0x0D && c[5] == 0x0A && c[6] == 0x1A && c[7] == 0x0A) return "image/png";
            return null;
        }
        
        public async Task<StepDownRequestDto> SaveLetterAsync(Guid requestId, string? fileName, byte[] content, StepDownActor actor)
        {
            RequireMaker(actor, "upload the request letter");
            var r = await LoadAsync(requestId);
            OnlyRequester(r, actor);
            Expect(r, StepDownStatus.Draft);
            if (content == null || content.Length == 0) throw new InvalidOperationException("The letter file is empty.");
            if (content.Length >= StepDownRouting.MaxLetterBytes)
                throw new InvalidOperationException($"The signed request letter must be smaller than 500 KB. This file is {content.Length / 1024.0:0.#} KB.");
            var name = System.IO.Path.GetFileName(fileName ?? string.Empty).Trim();
            var ext = System.IO.Path.GetExtension(name).ToLowerInvariant();
            var type = LetterContentType(ext, content);
            if (type == null) throw new InvalidOperationException("The letter must be a real PDF, JPG or PNG file.");
        
            var letter = await _context.ApplicationStepDownLetters.FirstOrDefaultAsync(l => l.RequestId == r.Id);
            var verb = letter == null ? "Uploaded" : "Replaced";
            if (letter == null)
            {
                letter = new ApplicationStepDownLetter { RequestId = r.Id };
                _context.ApplicationStepDownLetters.Add(letter);
            }
            letter.FileName = Clip(name, 200) ?? ("letter" + ext);
            letter.ContentType = type;
            letter.SizeBytes = content.Length;
            letter.Content = content;
            letter.UploadedById = actor.UserId;
            letter.UploadedAt = DateTime.UtcNow;
            await AuditAsync(r, StepDownAction.LetterUploaded, r.Status, r.Status, actor, null,
                $"{verb} signed request letter: {letter.FileName} ({content.Length / 1024.0:0.#} KB)");
            await SaveAsync();
            return (await GetAsync(r.Id))!;
        }
        
        public async Task<StepDownLetterFile?> GetLetterAsync(Guid requestId)
        {
            return await _context.ApplicationStepDownLetters.AsNoTracking()
                .Where(l => l.RequestId == requestId)
                .Select(l => new StepDownLetterFile { FileName = l.FileName, ContentType = l.ContentType, Content = l.Content })
                .FirstOrDefaultAsync();
        }
        
        // ---------- maker ----------
        public async Task<StepDownRequestDto> CreateAsync(Guid applicationId, string reason, StepDownActor actor)
        {
            RequireMaker(actor, "raise a step-down request");
            var why = RequiredText(reason, "A reason");

            // Archived applications are hidden by the query filter, so they are "not found" here.
            var app = await _context.Applications.FirstOrDefaultAsync(a => a.Id == applicationId);
            if (app == null) throw new InvalidOperationException("Application not found.");
            if (app.IsLocked) throw new InvalidOperationException(ApplicationLockGuard.LockedMessage);

            var open = await _context.ApplicationStepDownRequests
                .AnyAsync(x => x.ApplicationId == applicationId && StepDownStatus.Open.Contains(x.Status));
            if (open) throw new InvalidOperationException("A step-down request is already open for this application.");

            var r = new ApplicationStepDownRequest
            {
                ApplicationId = applicationId,
                Status = StepDownStatus.Draft,
                Reason = why,
                PmiCompanyName = Clip(app.PmiCompanyName, 200) ?? string.Empty,
                RequestedById = actor.UserId,
                CreatedAt = DateTime.UtcNow
            };
            _context.ApplicationStepDownRequests.Add(r);
            await AuditAsync(r, StepDownAction.Created, null, r.Status, actor, why);
            try { await _context.SaveChangesAsync(); }
            catch (DbUpdateException)
            {
                throw new InvalidOperationException("A step-down request is already open for this application.");
            }
            return (await GetAsync(r.Id))!;
        }

        public async Task<StepDownRequestDto> SubmitAsync(Guid requestId, StepDownActor actor)
        {
            RequireMaker(actor, "submit a step-down request");
            var r = await LoadAsync(requestId);
            OnlyRequester(r, actor);
            Expect(r, StepDownStatus.Draft);

            var app = await LoadApplicationAsync(r.ApplicationId);
            if (app.IsArchived) throw new InvalidOperationException(ApplicationLockGuard.ArchivedMessage);
            if (app.IsLocked) throw new InvalidOperationException(ApplicationLockGuard.LockedMessage);

            if (!await _context.ApplicationStepDownLetters.AnyAsync(l => l.RequestId == r.Id))
                throw new InvalidOperationException("Upload the signed request letter from the client before submitting this request.");
            var (needsHod, priorRun) = await DecideRouteAsync(r.Id, r.ApplicationId);
            r.RequiresHodApproval = needsHod;
            r.PriorStepDownCount = priorRun;
            var from = r.Status;
            r.Status = StepDownStatus.Submitted;
            r.SubmittedAt = DateTime.UtcNow;
            app.IsLocked = true;                 // locked from submission until the request is closed
            app.UpdatedAt = DateTime.UtcNow;
            await AuditAsync(r, StepDownAction.Submitted, from, r.Status, actor, r.Reason,
                needsHod
                    ? $"Route: maker, HOD Benefit Admin, checker ({priorRun} step-down(s) in a row for this RSA PIN)"
                    : $"Route: maker, checker only ({priorRun} step-down(s) in a row for this RSA PIN)");
            await SaveAsync();
            await NotifyAsync(StepDownAction.Submitted, r);
            return (await GetAsync(r.Id))!;
        }

        public async Task<StepDownRequestDto> CancelDraftAsync(Guid requestId, string? reason, StepDownActor actor)
        {
            RequireMaker(actor, "cancel a draft request");
            var r = await LoadAsync(requestId);
            OnlyRequester(r, actor);
            Expect(r, StepDownStatus.Draft);
            var from = r.Status;
            r.Status = StepDownStatus.Cancelled;
            await AuditAsync(r, StepDownAction.DraftCancelled, from, r.Status, actor, reason);
            await SaveAsync();
            return (await GetAsync(r.Id))!;
        }

        // "Delete" of a cancelled draft: the row stays for the audit trail, it only leaves the working lists.
        public async Task<StepDownRequestDto> RemoveDraftAsync(Guid requestId, StepDownActor actor)
        {
            RequireMaker(actor, "remove a cancelled draft");
            var r = await LoadAsync(requestId);
            OnlyRequester(r, actor);
            Expect(r, StepDownStatus.Cancelled);
            if (r.SubmittedAt != null)
                throw new InvalidOperationException("Only a cancelled draft that was never submitted can be removed.");
            var from = r.Status;
            r.Status = StepDownStatus.Removed;
            await AuditAsync(r, StepDownAction.DraftRemoved, from, r.Status, actor);
            await SaveAsync();
            return (await GetAsync(r.Id))!;
        }

        public async Task<StepDownRequestDto> RequestCancellationAsync(Guid requestId, string reason, StepDownActor actor)
        {
            RequireMaker(actor, "ask to cancel a request");
            var r = await LoadAsync(requestId);
            OnlyRequester(r, actor);
            Expect(r, StepDownStatus.Submitted, StepDownStatus.Approved);
            var why = RequiredText(reason, "A reason for cancelling");
            var from = r.Status;
            r.Status = StepDownStatus.CancellationRequested;
            r.CancellationRequestedAt = DateTime.UtcNow;
            r.CancelReason = why;
            await AuditAsync(r, StepDownAction.CancellationRequested, from, r.Status, actor, why);
            await SaveAsync();
            await NotifyAsync(StepDownAction.CancellationRequested, r);
            return (await GetAsync(r.Id))!;
        }

        // ---------- Head of Benefit Administration ----------
        public async Task<StepDownRequestDto> HodApproveAsync(Guid requestId, string? comment, StepDownActor actor)
        {
            Require(actor, StepDownRoles.Approver, "approve a step-down request");
            var r = await LoadAsync(requestId);
            Expect(r, StepDownStatus.Submitted);
            NotOwnRequest(r, actor, "approve");
            if (!r.RequiresHodApproval)
                throw new InvalidOperationException("This request goes straight to the Mortgage Lead and does not need HOD approval.");
            var from = r.Status;
            r.Status = StepDownStatus.Approved;
            r.DecidedById = actor.UserId;
            r.DecidedAt = DateTime.UtcNow;
            r.DecisionComment = Clip(comment, 1000);
            await AuditAsync(r, StepDownAction.Approved, from, r.Status, actor, comment);
            await SaveAsync();
            await NotifyAsync(StepDownAction.Approved, r);
            return (await GetAsync(r.Id))!;
        }

        public async Task<StepDownRequestDto> HodRejectAsync(Guid requestId, string comment, StepDownActor actor)
        {
            Require(actor, StepDownRoles.Approver, "reject a step-down request");
            var r = await LoadAsync(requestId);
            Expect(r, StepDownStatus.Submitted);
            if (!r.RequiresHodApproval)
                throw new InvalidOperationException("This request goes straight to the Mortgage Lead and does not need HOD approval.");
            NotOwnRequest(r, actor, "reject");
            var why = RequiredText(comment, "A reason for rejecting");
            var app = await LoadApplicationAsync(r.ApplicationId);
            var from = r.Status;
            r.Status = StepDownStatus.Rejected;
            r.DecidedById = actor.UserId;
            r.DecidedAt = DateTime.UtcNow;
            r.DecisionComment = why;
            app.IsLocked = false;
            app.UpdatedAt = DateTime.UtcNow;
            await AuditAsync(r, StepDownAction.Rejected, from, r.Status, actor, why);
            await SaveAsync();
            await NotifyAsync(StepDownAction.Rejected, r);
            return (await GetAsync(r.Id))!;
        }

        // ---------- Checker (MORTGAGE_LEAD): review, then step down or reject ----------
        public async Task<StepDownRequestDto> StepDownAsync(Guid requestId, string? comment, StepDownActor actor)
        {
            Require(actor, StepDownRoles.Checker, "step an application down");
            var r = await LoadAsync(requestId);
            ExpectWithChecker(r);
            if (!r.RequiresHodApproval)
            {
                // Another step-down for this RSA PIN may have completed since this was submitted. Then it needs the HOD after all.
                var route = await DecideRouteAsync(r.Id, r.ApplicationId);
                if (route.RequiresHod)
                    throw new InvalidOperationException("Another step-down for this RSA PIN was completed after this request was submitted, so it now needs HOD approval. Reject this request and raise a new one.");
            }
            NotOwnRequest(r, actor, "action");
            if (r.DecidedById == actor.UserId)
                throw new InvalidOperationException("The person who approved this request cannot also action it.");

            var app = await LoadApplicationAsync(r.ApplicationId);
            var from = r.Status;
            r.Status = StepDownStatus.SteppedDown;
            r.ActionedById = actor.UserId;
            r.ActionedAt = DateTime.UtcNow;
            r.ActionComment = Clip(comment, 1000);
            app.IsArchived = true;               // soft delete: hidden from normal queries, nothing removed
            // Release the reviewer assignment(s) so this application stops counting in anyone's workload. Nothing is deleted.
            var released = await _context.ApplicationAssignments.Where(x => x.ApplicationId == app.Id && x.IsActive).ToListAsync();
            foreach (var x in released) { x.IsActive = false; x.ReassignmentReason = "StepDown"; }
            app.IsLocked = false;
            app.UpdatedAt = DateTime.UtcNow;
            await AuditAsync(r, StepDownAction.SteppedDown, from, r.Status, actor, comment,
                $"Application {app.ApplicationNumber} archived; {released.Count} reviewer assignment(s) released");
            await SaveAsync();
            await NotifyAsync(StepDownAction.SteppedDown, r);
            return (await GetAsync(r.Id))!;
        }

        public async Task<StepDownRequestDto> CheckerRejectAsync(Guid requestId, string comment, StepDownActor actor)
        {
            Require(actor, StepDownRoles.Checker, "reject a step-down request");
            var r = await LoadAsync(requestId);
            ExpectWithChecker(r);
            NotOwnRequest(r, actor, "reject");
            var why = RequiredText(comment, "A reason for rejecting");
            var app = await LoadApplicationAsync(r.ApplicationId);
            var from = r.Status;
            r.Status = StepDownStatus.Rejected;
            r.ActionedById = actor.UserId;
            r.ActionedAt = DateTime.UtcNow;
            r.ActionComment = why;
            app.IsLocked = false;
            app.UpdatedAt = DateTime.UtcNow;
            await AuditAsync(r, StepDownAction.Rejected, from, r.Status, actor, why);
            await SaveAsync();
            await NotifyAsync(StepDownAction.Rejected, r);
            return (await GetAsync(r.Id))!;
        }

        // The person holding the request decides on a cancellation: the HOD before approval, the Checker after it.
        public async Task<StepDownRequestDto> DecideCancellationAsync(Guid requestId, bool accept, string? comment, StepDownActor actor)
        {
            var r = await LoadAsync(requestId);
            Expect(r, StepDownStatus.CancellationRequested);
            var afterApproval = r.DecidedById.HasValue;
            Require(actor, (afterApproval || !r.RequiresHodApproval) ? StepDownRoles.Checker : StepDownRoles.Approver, "decide this cancellation");
            NotOwnRequest(r, actor, "decide the cancellation of");

            var from = r.Status;
            if (accept)
            {
                var app = await LoadApplicationAsync(r.ApplicationId);
                r.Status = StepDownStatus.Cancelled;
                app.IsLocked = false;
                app.UpdatedAt = DateTime.UtcNow;
                await AuditAsync(r, StepDownAction.CancellationAccepted, from, r.Status, actor, comment);
            }
            else
            {
                r.Status = afterApproval ? StepDownStatus.Approved : StepDownStatus.Submitted;
                r.CancellationRequestedAt = null;
                await AuditAsync(r, StepDownAction.CancellationDeclined, from, r.Status, actor, comment);
            }
            await SaveAsync();
            await NotifyAsync(accept ? StepDownAction.CancellationAccepted : StepDownAction.CancellationDeclined, r);
            return (await GetAsync(r.Id))!;
        }

        // ---------- restore ----------
        public async Task<StepDownRequestDto> RestoreAsync(Guid requestId, string reason, StepDownActor actor)
        {
            Require(actor, StepDownRoles.Checker, "restore a stepped-down application");
            var r = await LoadAsync(requestId);
            Expect(r, StepDownStatus.SteppedDown);
            var why = RequiredText(reason, "A reason for restoring");
            var app = await LoadApplicationAsync(r.ApplicationId);

            // The RSA PIN can never be reused. While this application was archived a new one may have been
            // created with the same PIN. The filter hides other archived rows, so this finds only live ones.
            var clash = await _context.Applications.AnyAsync(a => a.Id != app.Id && a.RsaPin == app.RsaPin);
            if (clash)
                throw new InvalidOperationException("A newer application with the same RSA PIN exists. Deal with that one before restoring this application.");

            var from = r.Status;
            r.Status = StepDownStatus.Restored;
            r.RestoredById = actor.UserId;
            r.RestoredAt = DateTime.UtcNow;
            r.RestoreReason = why;
            app.IsArchived = false;
            var reactivated = await _context.ApplicationAssignments.Where(x => x.ApplicationId == app.Id && !x.IsActive && x.ReassignmentReason == "StepDown").ToListAsync();
            foreach (var x in reactivated) { x.IsActive = true; x.ReassignmentReason = "Restored after step-down"; }
            app.IsLocked = false;
            app.UpdatedAt = DateTime.UtcNow;
            await AuditAsync(r, StepDownAction.Restored, from, r.Status, actor, why,
                $"Application {app.ApplicationNumber} restored; {reactivated.Count} reviewer assignment(s) restored");
            await SaveAsync();
            await NotifyAsync(StepDownAction.Restored, r);
            return (await GetAsync(r.Id))!;
        }

        // ---------- reading ----------
        private IQueryable<StepDownRequestDto> Project(IQueryable<ApplicationStepDownRequest> q)
        {
            return from r in q
                   join a in _context.Applications.IgnoreQueryFilters() on r.ApplicationId equals a.Id
                   join u in _context.Users on r.RequestedById equals u.Id into uj
                   from u in uj.DefaultIfEmpty()
                   select new StepDownRequestDto
                   {
                       Id = r.Id,
                       ApplicationId = r.ApplicationId,
                       ApplicationNumber = a.ApplicationNumber,
                       BorrowerName = (a.FirstName + " " + a.LastName),
                       RsaPin = a.RsaPin,
                       IsHni = a.IsHni,
                       ApplicationStatus = a.Status,
                       ApplicationLocked = a.IsLocked,
                       ApplicationArchived = a.IsArchived,
                       LatestRequestId = _context.ApplicationStepDownRequests
                           .Where(x => x.ApplicationId == r.ApplicationId)
                           .OrderByDescending(x => x.CreatedAt).Select(x => (Guid?)x.Id).FirstOrDefault(),
                       LatestRequestStatus = _context.ApplicationStepDownRequests
                           .Where(x => x.ApplicationId == r.ApplicationId)
                           .OrderByDescending(x => x.CreatedAt).Select(x => x.Status).FirstOrDefault(),
                       LastSteppedDownAt = _context.ApplicationStepDownRequests
                           .Where(x => x.ApplicationId == r.ApplicationId && x.Status == StepDownStatus.SteppedDown)
                           .OrderByDescending(x => x.ActionedAt).Select(x => x.ActionedAt).FirstOrDefault(),
                       PmiCompanyName = r.PmiCompanyName,
                       Status = r.Status,
                       Reason = r.Reason,
                       RequiresHodApproval = r.RequiresHodApproval,
                       PriorStepDownCount = r.PriorStepDownCount,
                       HasLetter = _context.ApplicationStepDownLetters.Any(l => l.RequestId == r.Id),
                       LetterFileName = _context.ApplicationStepDownLetters.Where(l => l.RequestId == r.Id).Select(l => l.FileName).FirstOrDefault(),
                       LetterSizeBytes = _context.ApplicationStepDownLetters.Where(l => l.RequestId == r.Id).Select(l => (int?)l.SizeBytes).FirstOrDefault(),
                       LetterUploadedAt = _context.ApplicationStepDownLetters.Where(l => l.RequestId == r.Id).Select(l => (DateTime?)l.UploadedAt).FirstOrDefault(),
                       RequestedById = r.RequestedById,
                       RequestedByName = u != null ? (u.FirstName + " " + u.LastName) : string.Empty,
                       CreatedAt = r.CreatedAt,
                       SubmittedAt = r.SubmittedAt,
                       CancellationRequestedAt = r.CancellationRequestedAt,
                       CancelReason = r.CancelReason,
                       DecidedById = r.DecidedById,
                       DecidedAt = r.DecidedAt,
                       DecisionComment = r.DecisionComment,
                       ActionedById = r.ActionedById,
                       ActionedAt = r.ActionedAt,
                       ActionComment = r.ActionComment,
                       RestoredAt = r.RestoredAt,
                       RestoreReason = r.RestoreReason
                   };
        }

        private static StepDownRequestDto WithWaitingFor(StepDownRequestDto d)
        {
            d.WaitingFor = d.Status switch
            {
                StepDownStatus.Submitted => d.RequiresHodApproval ? StepDownRoles.Approver : StepDownRoles.Checker,
                StepDownStatus.Approved => StepDownRoles.Checker,
                StepDownStatus.CancellationRequested => (d.DecidedById.HasValue || !d.RequiresHodApproval) ? StepDownRoles.Checker : StepDownRoles.Approver,
                _ => null
            };
            return d;
        }

        public async Task<StepDownRequestDto?> GetAsync(Guid requestId)
        {
            var d = await Project(_context.ApplicationStepDownRequests.AsNoTracking().Where(x => x.Id == requestId))
                .FirstOrDefaultAsync();
            if (d != null && d.Status == StepDownStatus.Draft)
            {
                // A draft shows which route it will take when it is submitted.
                var route = await DecideRouteAsync(d.Id, d.ApplicationId);
                d.RequiresHodApproval = route.RequiresHod;
                d.PriorStepDownCount = route.Run;
            }
            return d == null ? null : WithWaitingFor(d);
        }

        public async Task<List<StepDownRequestDto>> ListAsync(string? scope, string? status, StepDownActor actor, int page, int pageSize)
        {
            if (page < 1) page = 1;
            if (pageSize < 1) pageSize = 50;
            if (pageSize > 100) pageSize = 100;

            IQueryable<ApplicationStepDownRequest> q = _context.ApplicationStepDownRequests.AsNoTracking();
            var s = (scope ?? string.Empty).Trim().ToLowerInvariant();
            // The Head of Benefit Administration sees everything by default (requested, approved, actioned).
            if (s.Length == 0) s = IsMaker(actor) ? "mine" : (Is(actor, StepDownRoles.Approver) ? "all" : "inbox");

            if (s == "mine")
            {
                q = q.Where(x => x.RequestedById == actor.UserId);
            }
            else if (s == "inbox")
            {
                if (Is(actor, StepDownRoles.Approver))
                    q = q.Where(x => (x.Status == StepDownStatus.Submitted && x.RequiresHodApproval)
                                     || (x.Status == StepDownStatus.CancellationRequested && x.DecidedById == null && x.RequiresHodApproval));
                else if (Is(actor, StepDownRoles.Checker))
                    q = q.Where(x => x.Status == StepDownStatus.Approved
                                     || (x.Status == StepDownStatus.Submitted && !x.RequiresHodApproval)
                                     || (x.Status == StepDownStatus.CancellationRequested && (x.DecidedById != null || !x.RequiresHodApproval)));
                else
                    q = q.Where(x => StepDownStatus.Open.Contains(x.Status));
            }
            // "all": every request, read only

            if (!string.IsNullOrWhiteSpace(status))
            {
                var st = status.Trim();
                q = q.Where(x => x.Status == st);
            }

            var page1 = q.OrderByDescending(x => x.CreatedAt).Skip((page - 1) * pageSize).Take(pageSize);
            var list = await Project(page1).ToListAsync();
            return list.OrderByDescending(x => x.CreatedAt).Select(WithWaitingFor).ToList();
        }

        public async Task<List<StepDownRequestDto>> ListForApplicationAsync(Guid applicationId)
        {
            var list = await Project(_context.ApplicationStepDownRequests.AsNoTracking()
                .Where(x => x.ApplicationId == applicationId)).ToListAsync();
            return list.OrderByDescending(x => x.CreatedAt).Select(WithWaitingFor).ToList();
        }

        // Counts per status for the dashboard cards.
        public async Task<Dictionary<string, int>> GetSummaryAsync()
        {
            return await _context.ApplicationStepDownRequests.AsNoTracking()
                .GroupBy(x => x.Status)
                .Select(g => new { Status = g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.Status, x => x.Count);
        }

        public async Task<List<StepDownAuditDto>> GetAuditAsync(Guid requestId)
        {
            return await _context.ApplicationStepDownAudits.AsNoTracking()
                .Where(x => x.RequestId == requestId)
                .OrderBy(x => x.PerformedAt)
                .Select(x => new StepDownAuditDto
                {
                    Id = x.Id,
                    RequestId = x.RequestId,
                    Action = x.Action,
                    FromStatus = x.FromStatus,
                    ToStatus = x.ToStatus,
                    PerformedByName = x.PerformedByName,
                    PerformedByRole = x.PerformedByRole,
                    PerformedAt = x.PerformedAt,
                    Comment = x.Comment,
                    Details = x.Details
                })
                .ToListAsync();
        }
    }
}
