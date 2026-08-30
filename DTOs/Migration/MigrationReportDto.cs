namespace MMGT.API.DTOs.Migration
{
    /// <summary>
    /// Comprehensive report of file migration from disk to database
    /// </summary>
    public class MigrationReportDto
    {
        /// <summary>
        /// Unique identifier for this migration run
        /// </summary>
        public Guid MigrationId { get; set; }

        /// <summary>
        /// Start time of migration
        /// </summary>
        public DateTime StartedAt { get; set; }

        /// <summary>
        /// End time of migration (null if still running)
        /// </summary>
        public DateTime? CompletedAt { get; set; }

        /// <summary>
        /// Current migration status: Running, Completed, Failed, Cancelled
        /// </summary>
        public string Status { get; set; } = "Running";

        /// <summary>
        /// Total files found on disk to migrate
        /// </summary>
        public int TotalFilesFound { get; set; }

        /// <summary>
        /// Number of files successfully migrated
        /// </summary>
        public int FilesSuccessfullyMigrated { get; set; }

        /// <summary>
        /// Number of files that were already in database
        /// </summary>
        public int FilesAlreadyMigrated { get; set; }

        /// <summary>
        /// Number of files that failed to migrate
        /// </summary>
        public int FilesFailed { get; set; }

        /// <summary>
        /// Number of files not found
        /// </summary>
        public int FilesNotFound { get; set; }

        /// <summary>
        /// Total data migrated in bytes
        /// </summary>
        public long TotalDataMigratedBytes { get; set; }

        /// <summary>
        /// Detailed results for each file
        /// </summary>
        public List<FileMigrationResultDto> FileResults { get; set; } = new();

        /// <summary>
        /// Overall error message if migration failed
        /// </summary>
        public string? ErrorMessage { get; set; }

        /// <summary>
        /// Percentage of migration complete (0-100)
        /// </summary>
        public int ProgressPercentage { get; set; }

        /// <summary>
        /// Duration of migration in seconds
        /// </summary>
        public double DurationSeconds => CompletedAt.HasValue 
            ? (CompletedAt.Value - StartedAt).TotalSeconds 
            : (DateTime.UtcNow - StartedAt).TotalSeconds;
    }
}
