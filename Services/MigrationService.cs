using MMGT.API.Services.Interfaces;
using MMGT.API.DTOs.Migration;
using MMGT.API.Data;
using Microsoft.EntityFrameworkCore;
using System.IO;
using System.Linq;

namespace MMGT.API.Services
{
    /// <summary>
    /// Service for migrating files from disk storage to database BLOB storage.
    /// Provides functionality to scan disk, read files, and store them as BLOBs.
    /// </summary>
    public class MigrationService : IMigrationService
    {
        private readonly ApplicationDbContext _context;
        private readonly IWebHostEnvironment _environment;
        private MigrationReportDto? _currentMigration;
        private bool _isMigrationRunning;

        public MigrationService(ApplicationDbContext context, IWebHostEnvironment environment)
        {
            _context = context;
            _environment = environment;
            _isMigrationRunning = false;
        }

        public async Task<bool> IsMigrationInProgressAsync()
        {
            return await Task.FromResult(_isMigrationRunning);
        }

        public async Task<MigrationReportDto> GetMigrationReportAsync()
        {
            if (_currentMigration != null)
            {
                return _currentMigration;
            }

            // Return empty report if no migration has run
            return new MigrationReportDto
            {
                MigrationId = Guid.NewGuid(),
                StartedAt = DateTime.UtcNow,
                Status = "NoMigration",
                TotalFilesFound = 0
            };
        }

        public async Task<MigrationReportDto> MigrateFilesFromDiskAsync()
        {
            if (_isMigrationRunning)
            {
                throw new Exception("Migration is already in progress");
            }

            _isMigrationRunning = true;
            var report = new MigrationReportDto
            {
                MigrationId = Guid.NewGuid(),
                StartedAt = DateTime.UtcNow,
                Status = "Running"
            };

            try
            {
                _currentMigration = report;

                // Get all disk files
                var uploadPath = Path.Combine(_environment.ContentRootPath ?? string.Empty, "wwwroot/uploads");

                if (!Directory.Exists(uploadPath))
                {
                    report.Status = "Completed";
                    report.CompletedAt = DateTime.UtcNow;
                    report.TotalFilesFound = 0;
                    return report;
                }

                // Get all files in the uploads directory recursively
                var diskFiles = Directory.GetFiles(uploadPath, "*.*", SearchOption.AllDirectories);
                report.TotalFilesFound = diskFiles.Length;

                if (diskFiles.Length == 0)
                {
                    report.Status = "Completed";
                    report.CompletedAt = DateTime.UtcNow;
                    return report;
                }

                // Get all ApplicationDocuments from database
                var dbDocuments = await _context.ApplicationDocuments
                    .Where(d => !string.IsNullOrWhiteSpace(d.FilePath))
                    .ToListAsync();

                var dbDocumentsByPath = dbDocuments
                    .GroupBy(d => (d.FilePath ?? string.Empty).ToLower())
                    .ToDictionary(g => g.Key, g => g.FirstOrDefault());

                // Migrate each file
                for (int i = 0; i < diskFiles.Length; i++)
                {
                    var diskFile = diskFiles[i];
                    report.ProgressPercentage = (int)((i + 1) / (double)diskFiles.Length * 100);

                    try
                    {
                        var fileResult = await MigrateFileAsync(diskFile, dbDocumentsByPath);
                        report.FileResults.Add(fileResult);

                        // Update counters
                        switch (fileResult.Status)
                        {
                            case "Success":
                                report.FilesSuccessfullyMigrated++;
                                report.TotalDataMigratedBytes += fileResult.FileSize;
                                break;
                            case "AlreadyMigrated":
                                report.FilesAlreadyMigrated++;
                                break;
                            case "NotFound":
                                report.FilesNotFound++;
                                break;
                            case "Error":
                                report.FilesFailed++;
                                break;
                        }
                    }
                    catch (Exception ex)
                    {
                        report.FilesFailed++;
                        // Continue with next file even if one fails
                    }
                }

                report.Status = "Completed";
                report.CompletedAt = DateTime.UtcNow;
            }
            catch (Exception ex)
            {
                report.Status = "Failed";
                report.CompletedAt = DateTime.UtcNow;
                report.ErrorMessage = ex.Message;
            }
            finally
            {
                _isMigrationRunning = false;
            }

            _currentMigration = report;
            return report;
        }

        public async Task<StorageStatisticsDto> GetStorageStatisticsAsync()
        {
            var stats = new StorageStatisticsDto();

            // Get database statistics
            var documents = await _context.ApplicationDocuments.ToListAsync();
            stats.TotalFilesInDatabase = documents.Count;

            var filesWithBlob = documents.Where(d => d.FileContent != null && d.FileContent.Length > 0).ToList();
            var filesWithPath = documents.Where(d => !string.IsNullOrWhiteSpace(d.FilePath)).ToList();

            stats.FilesWithBlobOnly = filesWithBlob.Count;
            stats.FilesWithDiskPath = filesWithPath.Count;
            stats.TotalBlobStorageBytes = filesWithBlob.Sum(d => d.FileContent?.Length ?? 0);

            if (filesWithBlob.Count > 0)
            {
                stats.AverageFileSizeBytes = (long)filesWithBlob.Average(d => d.FileSize);
                stats.LargestFileSizeBytes = filesWithBlob.Max(d => d.FileSize);
            }

            // Get disk statistics
            var uploadPath = Path.Combine(_environment.ContentRootPath ?? string.Empty, "wwwroot/uploads");

            if (Directory.Exists(uploadPath))
            {
                var diskFiles = Directory.GetFiles(uploadPath, "*.*", SearchOption.AllDirectories);
                stats.TotalFilesOnDisk = diskFiles.Length;
                stats.TotalDiskStorageBytes = diskFiles.Sum(f => new FileInfo(f).Length);

                // Find orphan files (files on disk but not in database)
                var diskFileSet = new HashSet<string>(diskFiles.Select(f => f.ToLower()));
                var dbFilePathSet = new HashSet<string>(
                    filesWithPath.Select(d => d.FilePath?.ToLower() ?? string.Empty)
                        .Where(p => !string.IsNullOrWhiteSpace(p))
                );

                stats.OrphanFilesOnDisk = diskFileSet.Count(f => !dbFilePathSet.Contains(f.ToLower()));
            }

            // Generate recommendation
            if (filesWithPath.Count > 0)
            {
                stats.Recommendation = $"Migrate {filesWithPath.Count} files from disk to database BLOB storage";
            }
            else if (stats.TotalFilesOnDisk > 0)
            {
                stats.Recommendation = $"Clean up {stats.TotalFilesOnDisk} orphan files from disk";
            }
            else
            {
                stats.Recommendation = "All files are in database. Storage is optimized.";
            }

            return stats;
        }

        /// <summary>
        /// Migrate a single file from disk to database
        /// </summary>
        private async Task<FileMigrationResultDto> MigrateFileAsync(
            string diskFilePath,
            Dictionary<string, Models.ApplicationDocument> dbDocumentsByPath)
        {
            var result = new FileMigrationResultDto
            {
                DiskPath = diskFilePath,
                FileName = Path.GetFileName(diskFilePath),
                MigratedAt = DateTime.UtcNow
            };

            try
            {
                // Check if file exists
                if (!File.Exists(diskFilePath))
                {
                    result.Status = "NotFound";
                    result.ErrorMessage = "File not found on disk";
                    return result;
                }

                // Normalize path for lookup (case-insensitive)
                var normalizedPath = diskFilePath.ToLower();

                // Check if there's a corresponding database record
                if (!dbDocumentsByPath.TryGetValue(normalizedPath, out var dbDoc) || dbDoc == null)
                {
                    result.Status = "NotFound";
                    result.ErrorMessage = "No database record found for this file";
                    return result;
                }

                // Validate required fields before proceeding
                if (dbDoc == null || string.IsNullOrWhiteSpace(dbDoc.FileName))
                {
                    result.Status = "Error";
                    result.ErrorMessage = "Document record is invalid (null or missing FileName)";
                    return result;
                }

                // Check if already migrated (FileContent exists)
                if (dbDoc.FileContent != null && dbDoc.FileContent.Length > 0)
                {
                    result.Status = "AlreadyMigrated";
                    result.DocumentId = dbDoc.Id;
                    result.ApplicationId = dbDoc.ApplicationId;
                    result.FileSize = dbDoc.FileSize;
                    return result;
                }

                // Read file from disk
                byte[] fileBytes = await File.ReadAllBytesAsync(diskFilePath);
                result.FileSize = fileBytes.Length;

                // Update database record with BLOB
                using (var transaction = await _context.Database.BeginTransactionAsync())
                {
                    try
                    {
                        var docId = dbDoc.Id;
                        var document = await _context.ApplicationDocuments.FirstOrDefaultAsync(d => d.Id == docId);

                        if (document == null)
                        {
                            result.Status = "Error";
                            result.ErrorMessage = "Database record not found during migration";
                            return result;
                        }

                        // Migrate to BLOB
                        document.FileContent = fileBytes;
                        // Optionally keep FilePath for reference or set to null
                        // For now, we keep it for backward compatibility
                        // document.FilePath = null;

                        await _context.SaveChangesAsync();
                        await transaction.CommitAsync();

                        result.Status = "Success";
                        result.DocumentId = document.Id;
                        result.ApplicationId = document.ApplicationId;
                    }
                    catch (Exception ex)
                    {
                        await transaction.RollbackAsync();
                        result.Status = "Error";
                        result.ErrorMessage = $"Database update failed: {ex.Message}";
                        return result;
                    }
                }

                return result;
            }
            catch (Exception ex)
            {
                result.Status = "Error";
                result.ErrorMessage = ex.Message;
                return result;
            }
        }
    }
}
