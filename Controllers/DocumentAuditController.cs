using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MMGT.API.Data;

namespace MMGT.API.Controllers
{
    /// <summary>Read-only history of document uploads, replacements, deletions and review decisions for an application.</summary>
    [ApiController]
    [Route("api/documentaudit")]
    [Authorize(Roles = "SUPER_ADMIN,ADMIN,LEAD_REVIEWER,SERVICE_MANAGER")]
    public class DocumentAuditController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        public DocumentAuditController(ApplicationDbContext context) { _context = context; }

        [HttpGet("application/{applicationId:guid}")]
        public async Task<IActionResult> ForApplication(Guid applicationId)
        {
            var events = await _context.ApplicationDocumentEvents.AsNoTracking()
                .Where(e => e.ApplicationId == applicationId)
                .OrderByDescending(e => e.PerformedAt)
                .Select(e => new
                {
                    e.Id, e.DocumentId, e.DocumentTypeId, e.DocumentTypeName, e.Action, e.PerformedById, e.PerformedByName,
                    e.PerformedByRole, e.PerformedAt, e.FileName, e.PreviousFileName, e.VersionNumber, e.Comment, e.ArchivedVersionId
                }).ToListAsync();
            var versions = await _context.ApplicationDocumentVersions.AsNoTracking()
                .Where(v => v.ApplicationId == applicationId)
                .OrderByDescending(v => v.ArchivedAt)
                .Select(v => new
                {
                    v.Id, v.DocumentId, v.DocumentTypeId, v.VersionNumber, v.FileName, v.FileType, v.FileSize, v.ReviewStatus,
                    v.ReviewComments, v.ReviewedBy, v.ReviewedAt, v.UploadedAt, v.ArchivedAt, v.ArchiveReason,
                    HasFile = v.FileContent != null
                }).ToListAsync();
            return Ok(new { events, versions });
        }

        [HttpGet("version/{versionId:guid}/download")]
        public async Task<IActionResult> DownloadVersion(Guid versionId)
        {
            var v = await _context.ApplicationDocumentVersions.AsNoTracking()
                .Where(x => x.Id == versionId)
                .Select(x => new { x.FileName, x.FileType, x.FileContent }).FirstOrDefaultAsync();
            if (v == null || v.FileContent == null || v.FileContent.Length == 0)
                return NotFound(new { message = "That earlier file is not available." });
            return File(v.FileContent, string.IsNullOrWhiteSpace(v.FileType) ? "application/octet-stream" : v.FileType, v.FileName);
        }
    }
}