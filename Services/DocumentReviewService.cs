using Microsoft.EntityFrameworkCore;
using MMGT.API.Data;
using MMGT.API.Models;

namespace MMGT.API.Services
{
    public interface IDocumentReviewService
    {
        Task<ApplicationDocument> ApproveDocumentAsync(Guid documentId, Guid reviewerId, string? comments);
        Task<ApplicationDocument> RejectDocumentAsync(Guid documentId, Guid reviewerId, string comments);
        Task<ApplicationDocument> RequestRevisionAsync(Guid documentId, Guid reviewerId, string comments);
        Task<bool> UpdateApplicationMandatoryStatusAsync(Guid applicationId);
    }

    public class DocumentReviewService : IDocumentReviewService
    {
        private readonly ApplicationDbContext _context;

        public DocumentReviewService(ApplicationDbContext context)
        {
            _context = context;
        }

        // An assigned reviewer (ADMIN) may review only applications assigned to them.
        // A Lead Reviewer may review any application, including overturning a reviewer's document rejection.
        // A Super Admin oversees only. (Locked applications are refused by the caller's lock guard.)
        private async Task EnsureMayReviewAsync(Guid applicationId, MMGT.API.Models.User reviewer)
        {
            if (reviewer.Role == "SUPER_ADMIN")
                throw new InvalidOperationException("Super Admins oversee applications and do not review documents. The assigned reviewer or a Lead Reviewer does.");
            if (reviewer.Role == "LEAD_REVIEWER") return;
            var mine = await _context.ApplicationAssignments.AnyAsync(a => a.ApplicationId == applicationId && a.ReviewerId == reviewer.Id && a.IsActive);
            if (!mine) throw new InvalidOperationException("This application is not assigned to you, so you cannot review its documents.");
        }
        
        public async Task<ApplicationDocument> ApproveDocumentAsync(Guid documentId, Guid reviewerId, string? comments)
        {
            var doc = await _context.ApplicationDocuments
                .Include(d => d.DocumentType)
                .FirstOrDefaultAsync(d => d.Id == documentId);

            if (doc == null) throw new InvalidOperationException("Document not found");
            await ApplicationLockGuard.EnsureOpenAsync(_context, doc.ApplicationId);

            var reviewer = await _context.Users.FindAsync(reviewerId);
            if (reviewer == null || !reviewer.IsActive || (reviewer.Role != "ADMIN" && reviewer.Role != "LEAD_REVIEWER" && reviewer.Role != "SUPER_ADMIN"))
                throw new InvalidOperationException("Reviewer not found or not authorized");
            await EnsureMayReviewAsync(doc.ApplicationId, reviewer);


            if (comments != null && comments.Trim().Length > 500) throw new InvalidOperationException("The comment must be 500 characters or fewer.");
            doc.ReviewStatus = "Approved";
            doc.ReviewComments = string.IsNullOrWhiteSpace(comments) ? null : comments.Trim();
            doc.ReviewedBy = reviewerId;
            doc.ReviewedAt = DateTime.UtcNow;
            await DocumentAudit.AddEventAsync(_context, doc.ApplicationId, doc, "Approved", reviewerId, doc.ReviewComments);

            _context.ApplicationDocuments.Update(doc);
            await _context.SaveChangesAsync();

            // Update application's MandatoryDocumentsComplete flag
            await UpdateApplicationMandatoryStatusAsync(doc.ApplicationId);

            return doc;
        }

        public async Task<ApplicationDocument> RejectDocumentAsync(Guid documentId, Guid reviewerId, string comments)
        {
            var doc = await _context.ApplicationDocuments
                .Include(d => d.DocumentType)
                .FirstOrDefaultAsync(d => d.Id == documentId);

            if (doc == null) throw new InvalidOperationException("Document not found");
            await ApplicationLockGuard.EnsureOpenAsync(_context, doc.ApplicationId);

            var reviewer = await _context.Users.FindAsync(reviewerId);
            if (reviewer == null || !reviewer.IsActive || (reviewer.Role != "ADMIN" && reviewer.Role != "LEAD_REVIEWER" && reviewer.Role != "SUPER_ADMIN"))
                throw new InvalidOperationException("Reviewer not found or not authorized");
            await EnsureMayReviewAsync(doc.ApplicationId, reviewer);

            var rejectReason = (comments ?? string.Empty).Trim();
            if (rejectReason.Length == 0) throw new InvalidOperationException("A comment is required when rejecting a document, so the client knows what to fix.");
            if (rejectReason.Length > 500) throw new InvalidOperationException("The comment must be 500 characters or fewer.");
            doc.ReviewStatus = "Rejected";
            doc.ReviewComments = rejectReason;
            doc.ReviewedBy = reviewerId;
            doc.ReviewedAt = DateTime.UtcNow;
            await DocumentAudit.AddEventAsync(_context, doc.ApplicationId, doc, "Rejected", reviewerId, rejectReason);

            _context.ApplicationDocuments.Update(doc);
            await _context.SaveChangesAsync();

            // Recalculate mandatory documents completeness
            await UpdateApplicationMandatoryStatusAsync(doc.ApplicationId);

            return doc;
        }

        public async Task<ApplicationDocument> RequestRevisionAsync(Guid documentId, Guid reviewerId, string comments)
        {
            var doc = await _context.ApplicationDocuments
                .Include(d => d.DocumentType)
                .FirstOrDefaultAsync(d => d.Id == documentId);

            if (doc == null) throw new InvalidOperationException("Document not found");
            await ApplicationLockGuard.EnsureOpenAsync(_context, doc.ApplicationId);

            var reviewer = await _context.Users.FindAsync(reviewerId);
            if (reviewer == null || !reviewer.IsActive || (reviewer.Role != "ADMIN" && reviewer.Role != "LEAD_REVIEWER" && reviewer.Role != "SUPER_ADMIN"))
                throw new InvalidOperationException("Reviewer not found or not authorized");
            await EnsureMayReviewAsync(doc.ApplicationId, reviewer);

            var revisionReason = (comments ?? string.Empty).Trim();
            if (revisionReason.Length == 0) throw new InvalidOperationException("A comment is required when requesting a revision, so the client knows what to change.");
            if (revisionReason.Length > 500) throw new InvalidOperationException("The comment must be 500 characters or fewer.");
            doc.ReviewStatus = "RequestedRevision";
            doc.ReviewComments = revisionReason;
            doc.ReviewedBy = reviewerId;
            doc.ReviewedAt = DateTime.UtcNow;
            await DocumentAudit.AddEventAsync(_context, doc.ApplicationId, doc, "RequestedRevision", reviewerId, revisionReason);

            _context.ApplicationDocuments.Update(doc);
            await _context.SaveChangesAsync();

            // Mark mandatory completeness accordingly
            await UpdateApplicationMandatoryStatusAsync(doc.ApplicationId);

            return doc;
        }

        /// <summary>
        /// Recalculates whether an application has all mandatory documents uploaded and approved.
        /// Sets Application.MandatoryDocumentsComplete accordingly.
        /// </summary>
        public async Task<bool> UpdateApplicationMandatoryStatusAsync(Guid applicationId)
        {
            var application = await _context.Applications.FindAsync(applicationId);
            if (application == null) throw new InvalidOperationException("Application not found");

            // Get active mandatory document types
            var mandatoryDocTypeIds = await _context.DocumentTypes
                .Where(dt => dt.IsActive && dt.IsMandatory)
                .Select(dt => dt.Id)
                .ToListAsync();

            // For each mandatory type, check for an approved latest version for this application
            foreach (var dtId in mandatoryDocTypeIds)
            {
                var hasApproved = await _context.ApplicationDocuments
                    .AnyAsync(d => d.ApplicationId == applicationId && d.DocumentTypeId == dtId && d.IsLatestVersion && d.ReviewStatus == "Approved");

                if (!hasApproved)
                {
                    application.MandatoryDocumentsComplete = false;
                    _context.Applications.Update(application);
                    await _context.SaveChangesAsync();
                    return false;
                }
            }

            application.MandatoryDocumentsComplete = true;
            _context.Applications.Update(application);
            await _context.SaveChangesAsync();
            return true;
        }
    }
}
