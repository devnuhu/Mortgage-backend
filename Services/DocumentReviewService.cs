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

        public async Task<ApplicationDocument> ApproveDocumentAsync(Guid documentId, Guid reviewerId, string? comments)
        {
            var doc = await _context.ApplicationDocuments
                .Include(d => d.DocumentType)
                .FirstOrDefaultAsync(d => d.Id == documentId);

            if (doc == null) throw new InvalidOperationException("Document not found");

            var reviewer = await _context.Users.FindAsync(reviewerId);
            if (reviewer == null || !reviewer.IsActive || (reviewer.Role != "ADMIN" && reviewer.Role != "LEAD_REVIEWER"))
                throw new InvalidOperationException("Reviewer not found or not authorized");

            // Mark other versions as not latest
            var others = await _context.ApplicationDocuments
                .Where(d => d.ApplicationId == doc.ApplicationId && d.DocumentTypeId == doc.DocumentTypeId && d.Id != doc.Id && d.IsLatestVersion)
                .ToListAsync();

            foreach (var o in others)
            {
                o.IsLatestVersion = false;
                _context.ApplicationDocuments.Update(o);
            }

            doc.ReviewStatus = "Approved";
            doc.ReviewComments = comments;
            doc.ReviewedBy = reviewerId;
            doc.ReviewedAt = DateTime.UtcNow;
            doc.IsLatestVersion = true;

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

            var reviewer = await _context.Users.FindAsync(reviewerId);
            if (reviewer == null || !reviewer.IsActive || (reviewer.Role != "ADMIN" && reviewer.Role != "LEAD_REVIEWER"))
                throw new InvalidOperationException("Reviewer not found or not authorized");

            doc.ReviewStatus = "Rejected";
            doc.ReviewComments = comments;
            doc.ReviewedBy = reviewerId;
            doc.ReviewedAt = DateTime.UtcNow;

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

            var reviewer = await _context.Users.FindAsync(reviewerId);
            if (reviewer == null || !reviewer.IsActive || (reviewer.Role != "ADMIN" && reviewer.Role != "LEAD_REVIEWER"))
                throw new InvalidOperationException("Reviewer not found or not authorized");

            doc.ReviewStatus = "RequestedRevision";
            doc.ReviewComments = comments;
            doc.ReviewedBy = reviewerId;
            doc.ReviewedAt = DateTime.UtcNow;

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
