namespace MMGT.API.Models
{
    /// <summary>Append-only log of every document upload, replacement, deletion and review decision. Rows are never updated or deleted.</summary>
    public class ApplicationDocumentEvent
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public Guid ApplicationId { get; set; }
        public Guid? DocumentId { get; set; }
        public Guid? DocumentTypeId { get; set; }
        public string? DocumentTypeName { get; set; }
        public string Action { get; set; } = string.Empty;   // Uploaded, Replaced, Deleted, Approved, Rejected, RequestedRevision
        public Guid PerformedById { get; set; }
        public string PerformedByName { get; set; } = string.Empty;
        public string PerformedByRole { get; set; } = string.Empty;
        public DateTime PerformedAt { get; set; } = DateTime.UtcNow;
        public string? FileName { get; set; }
        public string? PreviousFileName { get; set; }
        public int? VersionNumber { get; set; }
        public string? Comment { get; set; }
        public Guid? ArchivedVersionId { get; set; }
    }
}