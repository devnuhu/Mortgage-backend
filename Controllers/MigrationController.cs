using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using MMGT.API.Services.Interfaces;
using MMGT.API.DTOs.Migration;

namespace MMGT.API.Controllers
{
    /// <summary>
    /// Controller for managing file migrations from disk to database BLOB storage.
    /// All endpoints require SUPER_ADMIN role.
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    [Authorize(Roles = "SUPER_ADMIN")]
    public class MigrationController : ControllerBase
    {
        private readonly IMigrationService _migrationService;
        private readonly ILogger<MigrationController> _logger;

        public MigrationController(IMigrationService migrationService, ILogger<MigrationController> logger)
        {
            _migrationService = migrationService;
            _logger = logger;
        }

        /// <summary>
        /// Get the current migration status
        /// </summary>
        /// <returns>Migration report with current statistics</returns>
        [HttpGet("status")]
        public async Task<ActionResult<MigrationReportDto>> GetMigrationStatus()
        {
            try
            {
                var isRunning = await _migrationService.IsMigrationInProgressAsync();
                var report = await _migrationService.GetMigrationReportAsync();

                return Ok(new
                {
                    isRunning,
                    report
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting migration status");
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>
        /// Start migration of files from disk to database
        /// Scans wwwroot/uploads and migrates all files to BLOB storage
        /// This is a long-running operation - returns progress report
        /// </summary>
        /// <returns>Detailed migration report with per-file results</returns>
        [HttpPost("migrate-files")]
        public async Task<ActionResult<MigrationReportDto>> MigrateFiles()
        {
            try
            {
                var isRunning = await _migrationService.IsMigrationInProgressAsync();
                if (isRunning)
                {
                    return BadRequest(new { message = "Migration is already in progress. Please wait for it to complete." });
                }

                _logger.LogInformation("Starting file migration from disk to database");

                var report = await _migrationService.MigrateFilesFromDiskAsync();

                _logger.LogInformation(
                    $"Migration completed. Successfully migrated: {report.FilesSuccessfullyMigrated}, " +
                    $"Already migrated: {report.FilesAlreadyMigrated}, " +
                    $"Failed: {report.FilesFailed}, " +
                    $"Total data: {report.TotalDataMigratedBytes} bytes"
                );

                return Ok(report);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during file migration");
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>
        /// Get detailed migration report from last migration
        /// </summary>
        /// <returns>Complete migration report with all file details</returns>
        [HttpGet("report")]
        public async Task<ActionResult<MigrationReportDto>> GetMigrationReport()
        {
            try
            {
                var report = await _migrationService.GetMigrationReportAsync();
                return Ok(report);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting migration report");
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>
        /// Get current storage statistics
        /// Shows breakdown of files in database vs on disk
        /// Identifies orphan files and recommends optimizations
        /// </summary>
        /// <returns>Storage statistics and recommendations</returns>
        [HttpGet("statistics")]
        public async Task<ActionResult<StorageStatisticsDto>> GetStorageStatistics()
        {
            try
            {
                var stats = await _migrationService.GetStorageStatisticsAsync();
                return Ok(stats);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting storage statistics");
                return BadRequest(new { message = ex.Message });
            }
        }
    }
}
