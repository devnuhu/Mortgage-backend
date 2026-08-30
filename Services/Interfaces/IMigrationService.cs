using MMGT.API.DTOs.Migration;

namespace MMGT.API.Services.Interfaces
{
    /// <summary>
    /// Service for migrating files from disk storage to database BLOB storage.
    /// Allows legacy files to be consolidated into the database.
    /// </summary>
    public interface IMigrationService
    {
        /// <summary>
        /// Check if migration is currently in progress
        /// </summary>
        /// <returns>True if migration is running, false otherwise</returns>
        Task<bool> IsMigrationInProgressAsync();

        /// <summary>
        /// Start migration of files from disk to database
        /// Scans wwwroot/uploads directory and migrates all files to BLOB storage
        /// </summary>
        /// <returns>Migration report with statistics and results</returns>
        Task<MigrationReportDto> MigrateFilesFromDiskAsync();

        /// <summary>
        /// Get the current migration report/status
        /// Returns last completed migration or current migration progress
        /// </summary>
        /// <returns>Migration report with current statistics</returns>
        Task<MigrationReportDto> GetMigrationReportAsync();

        /// <summary>
        /// Get statistics about files still on disk vs in database
        /// </summary>
        /// <returns>Storage statistics</returns>
        Task<StorageStatisticsDto> GetStorageStatisticsAsync();
    }
}
