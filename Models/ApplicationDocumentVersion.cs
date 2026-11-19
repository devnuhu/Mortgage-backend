namespace MMGT.API.Models
{
    /// <summary>An earlier file of a document, kept with its review result when the document is replaced or deleted.</summary>
    public class ApplicationDocumentVersion
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public Guid ApplicationId { get; set; }
        public Guid DocumentId { get; set; }
        public Guid DocumentTypeId { get; set; }
        public int VersionNumber { get; set; }
        public string FileName { get; set; } = string.Empty;
        public string FileType { get; set; } = string.Empty;
        public long FileSize { get; set; }
        public byte[]? FileContent { get; set; }
        public string ReviewStatus { get; set; } = string.Empty;
        public string? ReviewComments { get; set; }
        public Guid? ReviewedBy { get; set; }
        public DateTime? ReviewedAt { get; set; }
        public Guid UploadedBy { get; set; }
        public DateTime UploadedAt { get; set; }
        public DateTime ArchivedAt { get; set; } = DateTime.UtcNow;
        public Guid ArchivedBy { get; set; }
        public string ArchiveReason { get; set; } = string.Empty;   // Replaced or Deleted
    }
}