namespace MMGT.API.DTOs.Migration
{
    /// <summary>
    /// Statistics about current storage across disk and database
    /// </summary>
    public class StorageStatisticsDto
    {
        /// <summary>
        /// Total files in database with BLOB content
        /// </summary>
        public int TotalFilesInDatabase { get; set; }

        /// <summary>
        /// Total BLOB data stored in database (bytes)
        /// </summary>
        public long TotalBlobStorageBytes { get; set; }

        /// <summary>
        /// Number of database records with FilePath (legacy disk references)
        /// </summary>
        public int FilesWithDiskPath { get; set; }

        /// <summary>
        /// Number of database records with null FilePath (BLOB-only)
        /// </summary>
        public int FilesWithBlobOnly { get; set; }

        /// <summary>
        /// Total files found in wwwroot/uploads directory
        /// </summary>
        public int TotalFilesOnDisk { get; set; }

        /// <summary>
        /// Total disk storage used (bytes)
        /// </summary>
        public long TotalDiskStorageBytes { get; set; }

        /// <summary>
        /// Number of files on disk that don't have database records
        /// </summary>
        public int OrphanFilesOnDisk { get; set; }

        /// <summary>
        /// Average file size in bytes
        /// </summary>
        public long AverageFileSizeBytes { get; set; }

        /// <summary>
        /// Largest file size in bytes
        /// </summary>
        public long LargestFileSizeBytes { get; set; }

        /// <summary>
        /// Recommended storage optimization (e.g., "Migrate 50 files")
        /// </summary>
        public string Recommendation { get; set; } = string.Empty;

        /// <summary>
        /// Last migration timestamp
        /// </summary>
        public DateTime? LastMigrationTime { get; set; }
    }
}
