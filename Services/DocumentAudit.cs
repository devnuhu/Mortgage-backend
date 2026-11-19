using Microsoft.EntityFrameworkCore;
using MMGT.API.Data;
using MMGT.API.Models;

namespace MMGT.API.Services
{
    /// <summary>Writes the document audit rows. Rows are added to the context and saved together with the change itself.</summary>
    public static class DocumentAudit
    {
        private static string? Clip(string? s, int max)
        {
            if (string.IsNullOrWhiteSpace(s)) return null;
            s = s.Trim();
            return s.Length <= max ? s : s.Substring(0, max);
        }

        public static async Task AddEventAsync(ApplicationDbContext ctx, Guid applicationId, ApplicationDocument doc, string action, Guid by,
            string? comment, string? fileName = null, string? previousFileName = null, Guid? archivedVersionId = null)
        {
            var u = await ctx.Users.AsNoTracking().Where(x => x.Id == by)
                .Select(x => new { x.FirstName, x.LastName, x.Role }).FirstOrDefaultAsync();
            var typeName = !string.IsNullOrWhiteSpace(doc.CustomName) ? doc.CustomName : doc.DocumentType?.Name;
            if (string.IsNullOrWhiteSpace(typeName))
                typeName = await ctx.DocumentTypes.AsNoTracking().Where(t => t.Id == doc.DocumentTypeId).Select(t => t.Name).FirstOrDefaultAsync();
            ctx.ApplicationDocumentEvents.Add(new ApplicationDocumentEvent
            {
                ApplicationId = applicationId,
                DocumentId = doc.Id,
                DocumentTypeId = doc.DocumentTypeId,
                DocumentTypeName = Clip(typeName, 200),
                Action = action,
                PerformedById = by,
                PerformedByName = u == null ? "Unknown user" : Clip((u.FirstName + " " + u.LastName).Trim(), 200) ?? "Unknown user",
                PerformedByRole = u?.Role ?? string.Empty,
                PerformedAt = DateTime.UtcNow,
                FileName = Clip(fileName ?? doc.FileName, 255),
                PreviousFileName = Clip(previousFileName, 255),
                VersionNumber = doc.Version,
                Comment = Clip(comment, 1000),
                ArchivedVersionId = archivedVersionId
            });
        }

        // Copies the CURRENT file of a document, with its review result, into a version row.
        public static async Task<ApplicationDocumentVersion> ArchiveAsync(ApplicationDbContext ctx, ApplicationDocument doc, Guid by, string reason)
        {
            var docId = (Guid?)doc.Id;
            var lastWrite = await ctx.ApplicationDocumentEvents.AsNoTracking()
                .Where(e => e.DocumentId == docId && (e.Action == "Uploaded" || e.Action == "Replaced"))
                .OrderByDescending(e => e.PerformedAt).Select(e => (DateTime?)e.PerformedAt).FirstOrDefaultAsync();
            return new ApplicationDocumentVersion
            {
                ApplicationId = doc.ApplicationId,
                DocumentId = doc.Id,
                DocumentTypeId = doc.DocumentTypeId,
                VersionNumber = doc.Version,
                FileName = doc.FileName,
                FileType = doc.FileType,
                FileSize = doc.FileSize,
                FileContent = doc.FileContent,
                ReviewStatus = doc.ReviewStatus,
                ReviewComments = doc.ReviewComments,
                ReviewedBy = doc.ReviewedBy,
                ReviewedAt = doc.ReviewedAt,
                UploadedBy = doc.UploadedBy,
                UploadedAt = lastWrite ?? doc.CreatedAt,
                ArchivedAt = DateTime.UtcNow,
                ArchivedBy = by,
                ArchiveReason = reason
            };
        }
    }
}