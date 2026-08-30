namespace MMGT.API.DTOs.File
{
    public class FileUploadResponse
    {
        public Guid Id { get; set; }
        public Guid ApplicationId { get; set; }
        public string FileName { get; set; } = string.Empty;
        public string FileType { get; set; } = string.Empty;
        public string DocumentType { get; set; } = string.Empty;
        public long FileSize { get; set; }
        public string FilePath { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }

        /// <summary>
        /// Indicates if the file is stored as BLOB in the database (true) or on disk (false)
        /// </summary>
        public bool IsBlobStored { get; set; } = false;

        /// <summary>
        /// Indicates if this is a legacy document from the old disk storage system
        /// Legacy documents are read-only and cannot be replaced or deleted
        /// </summary>
        public bool IsLegacy { get; set; } = false;

        /// <summary>
        /// The updated application status after file upload
        /// Tells frontend if application status changed (e.g., from "rejected" to "resubmitted")
        /// </summary>
        public string ApplicationStatus { get; set; } = string.Empty;

        /// <summary>
        /// Indicates if the document can be deleted by the user
        /// Only new (non-legacy) documents can be deleted
        /// </summary>
        public bool CanDelete => !IsLegacy;

        /// <summary>
        /// Indicates if the document can be replaced by the user
        /// Only new (non-legacy) documents can be replaced
        /// </summary>
        public bool CanReplace => !IsLegacy;

        /// <summary>
        /// Human-readable storage type description
        /// </summary>
        public string StorageType => IsLegacy ? "Disk (Legacy - Read-Only)" : "Database (BLOB - Editable)";
    }
}