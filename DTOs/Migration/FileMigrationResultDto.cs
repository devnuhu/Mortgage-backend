namespace MMGT.API.DTOs.Migration
{
    /// <summary>
    /// Represents the result of migrating a single file
    /// </summary>
    public class FileMigrationResultDto
    {
        /// <summary>
        /// The ApplicationDocument ID
        /// </summary>
        public Guid DocumentId { get; set; }

        /// <summary>
        /// The application this file belongs to
        /// </summary>
        public Guid ApplicationId { get; set; }

        /// <summary>
        /// Original filename
        /// </summary>
        public string FileName { get; set; } = string.Empty;

        /// <summary>
        /// Path where file was located on disk
        /// </summary>
        public string DiskPath { get; set; } = string.Empty;

        /// <summary>
        /// File size in bytes
        /// </summary>
        public long FileSize { get; set; }

        /// <summary>
        /// Migration status: Success, AlreadyMigrated, Error, NotFound
        /// </summary>
        public string Status { get; set; } = "Unknown";

        /// <summary>
        /// Error message if migration failed
        /// </summary>
        public string? ErrorMessage { get; set; }

        /// <summary>
        /// Timestamp when migration occurred
        /// </summary>
        public DateTime MigratedAt { get; set; }
    }
}
