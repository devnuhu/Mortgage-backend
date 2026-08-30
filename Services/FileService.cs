using MMGT.API.Services.Interfaces;
using MMGT.API.DTOs.File;
using MMGT.API.Data;
using MMGT.API.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Linq;
using System.IO;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using iText.Kernel.Pdf;
using iText.Kernel.Pdf.Canvas.Draw;
using iText.IO.Exceptions;
using iText.Kernel.Exceptions;
using iText.Kernel.Utils;

namespace MMGT.API.Services
{
    public class FileService : IFileService
    {
        private readonly ApplicationDbContext _context;
        private readonly IConfiguration _configuration;
        private readonly IWebHostEnvironment _environment;
        private readonly ILogger<FileService> _logger;

        public FileService(ApplicationDbContext context, IConfiguration configuration, IWebHostEnvironment environment, ILogger<FileService> logger)
        {
            _context = context;
            _configuration = configuration;
            _environment = environment;
            _logger = logger;
        }

        private string SanitizeFileName(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return string.Empty;
            var invalid = Path.GetInvalidFileNameChars();
            var cleaned = new string(name.Where(c => !invalid.Contains(c)).ToArray());
            cleaned = cleaned.Replace(' ', '_');
            return cleaned;
        }

        private string GetStoredFileName(string rsaPin, string originalFileName)
        {
            var ext = Path.GetExtension(originalFileName).ToLowerInvariant();
            var baseName = SanitizeFileName(rsaPin ?? string.Empty);
            if (string.IsNullOrWhiteSpace(baseName)) baseName = "file";
            return $"{baseName}_{Guid.NewGuid()}{ext}";
        }

        /// <summary>
        /// Resolves a legacy document file path for disk storage.
        /// Handles multiple path formats:
        /// - Absolute paths (existing legacy format from old server)
        /// - Relative paths (uploads/...)
        /// - wwwroot-prefixed paths
        /// 
        /// Returns the resolved physical path that should exist on the current server.
        /// </summary>
        private string ResolveLegacyFilePath(string storedPath, bool isLegacy = true)
        {
            if (string.IsNullOrWhiteSpace(storedPath))
                return string.Empty;

            // Normalize path separators to backslash for Windows compatibility
            string normalizedPath = storedPath.Replace('/', '\\');

            // Case 1: Absolute path (e.g., C:\inetpub\wwwroot\...\uploads\...)
            if (Path.IsPathRooted(normalizedPath))
            {
                // Extract the relative portion after the old server identifier
                // Old format: C:\inetpub\wwwroot\backend\AccessMortage\wwwroot\uploads\{appId}\{file}
                // We need to find where 'wwwroot\uploads' starts and build from there

                string[] pathParts = normalizedPath.Split('\\');
                int wwwrootIndex = -1;

                // Find the index of 'wwwroot' that precedes 'uploads'
                for (int i = 0; i < pathParts.Length - 1; i++)
                {
                    if (pathParts[i].Equals("wwwroot", StringComparison.OrdinalIgnoreCase) &&
                        i + 1 < pathParts.Length &&
                        pathParts[i + 1].Equals("uploads", StringComparison.OrdinalIgnoreCase))
                    {
                        wwwrootIndex = i;
                        break;
                    }
                }

                if (wwwrootIndex >= 0)
                {
                    // Reconstruct path from wwwroot onwards using current server's WebRootPath
                    var relativeParts = new List<string>(pathParts.Skip(wwwrootIndex + 1)); // Skip 'wwwroot', keep 'uploads' onwards
                    var relativePath = string.Join("\\", relativeParts);
                    string reconstructedPath = Path.Combine(_environment.WebRootPath, relativePath);

                    // Check if reconstructed path exists
                    if (File.Exists(reconstructedPath))
                    {
                        return reconstructedPath;
                    }

                    // If not found and document is legacy, try path mapping fallback
                    if (isLegacy)
                    {
                        string mappedPath = ApplyLegacyPathMapping(normalizedPath);
                        if (!string.IsNullOrWhiteSpace(mappedPath) && mappedPath != normalizedPath)
                        {
                            _logger.LogInformation(
                                "Legacy document path mapping applied. DocumentPath: {OriginalPath}, MappedPath: {MappedPath}, ReconstructedPath: {ReconstructedPath}",
                                normalizedPath, mappedPath, reconstructedPath);
                            return mappedPath;
                        }
                    }

                    return reconstructedPath;
                }

                // If we can't find the pattern, try path mapping fallback for legacy documents
                if (isLegacy)
                {
                    string mappedPath = ApplyLegacyPathMapping(normalizedPath);
                    if (!string.IsNullOrWhiteSpace(mappedPath) && mappedPath != normalizedPath)
                    {
                        _logger.LogInformation(
                            "Legacy document path mapping applied (pattern not found). OriginalPath: {OriginalPath}, MappedPath: {MappedPath}",
                            normalizedPath, mappedPath);
                        return mappedPath;
                    }
                }

                // Return as-is (might fail, but provides diagnostic data)
                return normalizedPath;
            }

            // Case 2: Relative path starting with 'uploads' or 'wwwroot'
            if (normalizedPath.StartsWith("uploads\\", StringComparison.OrdinalIgnoreCase))
            {
                return Path.Combine(_environment.WebRootPath, normalizedPath);
            }

            if (normalizedPath.StartsWith("wwwroot\\", StringComparison.OrdinalIgnoreCase))
            {
                // Remove 'wwwroot\' prefix and combine with current WebRootPath
                string withoutWwwroot = normalizedPath.Substring("wwwroot\\".Length);
                return Path.Combine(_environment.WebRootPath, withoutWwwroot);
            }

            // Case 3: Other relative paths - combine with WebRootPath
            return Path.Combine(_environment.WebRootPath, normalizedPath);
        }

        /// <summary>
        /// Applies configured legacy storage path mapping to handle server migrations.
        /// Replaces OldServerRoot with NewServerRoot if configured.
        /// 
        /// Example:
        /// Input: C:\inetpub\wwwroot\backend\AccessMortage\wwwroot\uploads\{appId}\{file}
        /// Output: C:\inetpub\wwwroot\Mortgage\wwwroot\uploads\{appId}\{file}
        /// </summary>
        private string ApplyLegacyPathMapping(string originalPath)
        {
            var oldRoot = _configuration["LegacyStoragePaths:OldServerRoot"];
            var newRoot = _configuration["LegacyStoragePaths:NewServerRoot"];

            // If configuration is not set, return original path unchanged
            if (string.IsNullOrWhiteSpace(oldRoot) || string.IsNullOrWhiteSpace(newRoot))
                return originalPath;

            // Case-insensitive comparison for path matching
            if (originalPath.StartsWith(oldRoot, StringComparison.OrdinalIgnoreCase))
            {
                // Replace old root with new root
                string mappedPath = newRoot + originalPath.Substring(oldRoot.Length);

                _logger.LogInformation(
                    "Legacy storage path mapping: OldRoot={OldRoot}, NewRoot={NewRoot}, OriginalPath={OriginalPath}, MappedPath={MappedPath}",
                    oldRoot, newRoot, originalPath, mappedPath);

                return mappedPath;
            }

            return originalPath;
        }

        /// <summary>
        /// Normalizes invalid FileType values to ensure they are valid MIME types.
        /// This handles legacy data corruption where FileType='Legacy' was imported from old systems.
        /// 
        /// Fallback to 'application/pdf' if:
        /// - FileType is null
        /// - FileType is empty or whitespace
        /// - FileType does not contain '/' (invalid MIME format)
        /// - FileType contains 'Legacy' (known corrupted value)
        /// 
        /// Logs the normalization event with DocumentId, original FileType, and resolved FileType.
        /// </summary>
        private string NormalizeFileType(Guid documentId, string fileType)
        {
            const string DEFAULT_MIME_TYPE = "application/pdf";

            // Check if FileType is null, empty, or whitespace
            if (string.IsNullOrWhiteSpace(fileType))
            {
                _logger.LogWarning(
                    "Invalid FileType detected for document {DocumentId}: null/empty/whitespace. Fallback: {ResolvedFileType}",
                    documentId, DEFAULT_MIME_TYPE);
                return DEFAULT_MIME_TYPE;
            }

            // Trim whitespace for further checks
            string trimmedFileType = fileType.Trim();

            // Check if FileType contains 'Legacy' (case-insensitive)
            if (trimmedFileType.Contains("Legacy", StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogWarning(
                    "Invalid FileType detected for document {DocumentId}: contains 'Legacy'. Original: {OriginalFileType}, Fallback: {ResolvedFileType}",
                    documentId, trimmedFileType, DEFAULT_MIME_TYPE);
                return DEFAULT_MIME_TYPE;
            }

            // Check if FileType contains '/' (valid MIME type format: type/subtype)
            if (!trimmedFileType.Contains("/"))
            {
                _logger.LogWarning(
                    "Invalid FileType detected for document {DocumentId}: does not contain '/'. Original: {OriginalFileType}, Fallback: {ResolvedFileType}",
                    documentId, trimmedFileType, DEFAULT_MIME_TYPE);
                return DEFAULT_MIME_TYPE;
            }

            // FileType appears valid, return as-is
            return trimmedFileType;
        }

        public async Task<FileUploadResponse> UploadFileAsync(IFormFile file, Guid applicationId, string documentType, Guid performedById)
        {
            var application = await _context.Applications.FindAsync(applicationId);
            if (application == null)
                throw new Exception("Application not found");

            if (file == null || file.Length == 0)
                throw new Exception("No file provided");

            // Validate file extension - only PDF allowed
            var allowedExtensions = _configuration.GetSection("FileStorage:AllowedExtensions").Get<string[]>() ?? new[] { ".pdf" };
            var fileExtension = Path.GetExtension(file.FileName).ToLowerInvariant();
            if (!allowedExtensions.Contains(fileExtension))
                throw new Exception($"File type {fileExtension} is not allowed. Only PDF files are permitted.");

            // Look up DocumentType by code to get DocumentTypeId
            var docType = await _context.DocumentTypes.FirstOrDefaultAsync(dt => dt.Code == documentType);
            if (docType == null)
                throw new Exception($"Document type '{documentType}' not found. Please use a valid document type code.");

            // Validate file size: use document-type-specific limit
            int maxFileSizeKB = docType.MaxFileSizeKB;
            long maxFileSizeBytes = (long)maxFileSizeKB * 1024;
            if (file.Length > maxFileSizeBytes)
                throw new Exception($"File size exceeds maximum allowed size of {maxFileSizeKB}KB for {documentType}. Uploaded file is {file.Length / 1024}KB.");

            // Read file bytes for BLOB storage only
            byte[] fileBytes;
            using (var memoryStream = new MemoryStream())
            {
                await file.CopyToAsync(memoryStream);
                fileBytes = memoryStream.ToArray();
            }

            var storedFileName = GetStoredFileName(application.RsaPin ?? string.Empty, file.FileName);

            // Use execution strategy to handle transient failures within manual transactions
            var strategy = _context.Database.CreateExecutionStrategy();
            await strategy.ExecuteAsync(async () =>
            {
                using (var transaction = await _context.Database.BeginTransactionAsync())
                {
                    try
                    {
                        var applicationDocument = new ApplicationDocument
                        {
                            Id = Guid.NewGuid(),
                            FileName = storedFileName,
                            FilePath = null,  // New uploads are BLOB-only, no disk storage
                            FileContent = fileBytes,  // Store BLOB in database
                            FileType = file.ContentType,
                            DocumentTypeId = docType.Id,  // Set required DocumentTypeId
                            LegacyDocumentType = documentType,
                            FileSize = file.Length,
                            ApplicationId = applicationId,
                            CreatedAt = DateTime.UtcNow,
                            UploadedBy = performedById,
                            ReviewStatus = "Pending"
                        };

                        _context.ApplicationDocuments.Add(applicationDocument);

                        // Do NOT change application status on document upload
                        // Application status should only change when:
                        // 1. Reviewer explicitly updates via PUT /api/applications/{id}/status
                        // 2. Client explicitly submits via POST /api/applications/{id}/submit
                        // Document upload only creates ApplicationDocument with ReviewStatus = "Pending"

                        await _context.SaveChangesAsync();
                        await transaction.CommitAsync();
                    }
                    catch
                    {
                        await transaction.RollbackAsync();
                        throw;
                    }
                }
            });

            // Fetch the updated application status to return to frontend
            var updatedApp = await _context.Applications.FindAsync(applicationId);
            var newApplicationStatus = updatedApp?.Status ?? string.Empty;

            return new FileUploadResponse
            {
                FileName = storedFileName,
                FilePath = null,  // BLOB-only uploads don't have disk path
                FileType = file.ContentType,
                FileSize = file.Length,
                IsBlobStored = true,
                ApplicationStatus = newApplicationStatus  // Include updated status
            };
        }

        // Get a single file metadata - OPTIMIZED: Excludes FileContent BLOB from query, includes ApplicationStatus
        public async Task<FileUploadResponse> GetFileAsync(Guid fileId)
        {
            var doc = await _context.ApplicationDocuments
                .AsNoTracking()
                .Where(d => d.Id == fileId)
                .Join(_context.Applications.AsNoTracking(),
                    ad => ad.ApplicationId,
                    app => app.Id,
                    (ad, app) => new FileUploadResponse
                    {
                        Id = ad.Id,
                        ApplicationId = ad.ApplicationId,
                        FileName = ad.FileName,
                        FilePath = ad.FilePath ?? string.Empty,
                        FileType = ad.FileType,
                        DocumentType = ad.LegacyDocumentType ?? string.Empty,
                        FileSize = ad.FileSize,
                        IsBlobStored = ad.FileContent != null && ad.FileContent.Length > 0,
                        ApplicationStatus = app.Status ?? string.Empty
                    })
                .FirstOrDefaultAsync();

            if (doc == null)
                throw new Exception("File not found");

            // Normalize invalid FileType values before returning metadata
            // This prevents ASP.NET Core from rejecting invalid MIME types in response headers
            doc.FileType = NormalizeFileType(doc.Id, doc.FileType);

            return doc;
        }

        // Get files by application - OPTIMIZED: Excludes FileContent BLOB from query, includes ApplicationStatus
        public async Task<List<FileUploadResponse>> GetFilesByApplicationAsync(Guid applicationId)
        {
            // Fetch application status once for performance (all docs belong to same app)
            var appStatus = await _context.Applications
                .AsNoTracking()
                .Where(a => a.Id == applicationId)
                .Select(a => a.Status ?? string.Empty)
                .FirstOrDefaultAsync();

            // Fetch all documents for the application
            var docs = await _context.ApplicationDocuments
                .AsNoTracking()
                .Where(d => d.ApplicationId == applicationId)
                .Select(doc => new FileUploadResponse
                {
                    Id = doc.Id,
                    ApplicationId = doc.ApplicationId,
                    FileName = doc.FileName,
                    FilePath = doc.FilePath ?? string.Empty,
                    FileType = doc.FileType,
                    DocumentType = doc.LegacyDocumentType ?? string.Empty,
                    FileSize = doc.FileSize,
                    IsBlobStored = doc.FileContent != null && doc.FileContent.Length > 0,
                    IsLegacy = doc.IsLegacy,
                    ApplicationStatus = appStatus  // Set application status for all documents
                })
                .ToListAsync();

            // Normalize invalid FileType values for all documents
            // This ensures consistent MIME types across all document metadata
            foreach (var doc in docs)
            {
                doc.FileType = NormalizeFileType(doc.Id, doc.FileType);
            }

            return docs;
        }

        // Delete a file (removes BLOB from database)
        // Legacy (disk-stored) documents are READ-ONLY and cannot be deleted
        public async Task<bool> DeleteFileAsync(Guid fileId, Guid performedById)
        {
            var doc = await _context.ApplicationDocuments.FirstOrDefaultAsync(d => d.Id == fileId);
            if (doc == null) return false;

            // PROTECTION: Prevent deletion of legacy documents
            if (doc.IsLegacy || (!string.IsNullOrWhiteSpace(doc.FilePath) && 
                (doc.FileContent == null || doc.FileContent.Length == 0)))
            {
                throw new Exception("Cannot delete legacy documents. This file is from the old system and is read-only. " +
                    "Only documents stored in the new system can be deleted.");
            }

            // Verify this is a BLOB file (new system)
            if (doc.FileContent == null || doc.FileContent.Length == 0)
            {
                throw new Exception("No file content found. Cannot delete file.");
            }

            _context.ApplicationDocuments.Remove(doc);
            await _context.SaveChangesAsync();
            return true;
        }

        // Download file bytes - prefers BLOB, falls back to disk
        public async Task<byte[]> DownloadFileAsync(Guid fileId)
        {
            var doc = await _context.ApplicationDocuments.FirstOrDefaultAsync(d => d.Id == fileId);
            if (doc == null) throw new Exception("File not found");

            // Prefer BLOB storage if available
            if (doc.FileContent != null && doc.FileContent.Length > 0)
            {
                return doc.FileContent;
            }

            // Fallback to disk storage for legacy files
            if (string.IsNullOrWhiteSpace(doc.FilePath))
                throw new Exception("File not found on disk");

            // Resolve the legacy file path to current server location
            string resolvedPath = ResolveLegacyFilePath(doc.FilePath, isLegacy: doc.IsLegacy);

            if (!File.Exists(resolvedPath))
            {
                // Log diagnostic information for troubleshooting
                _logger.LogWarning(
                    "Legacy document file not found. DocumentId: {DocumentId}, StoredPath: {StoredPath}, ResolvedPath: {ResolvedPath}",
                    fileId,
                    doc.FilePath,
                    resolvedPath);

                throw new Exception("File not found on disk");
            }

            return await File.ReadAllBytesAsync(resolvedPath);
        }

        // Replace an existing file atomically
        // Legacy (disk-stored) documents are READ-ONLY and cannot be replaced
        public async Task<FileUploadResponse> ReplaceFileAsync(Guid existingFileId, IFormFile newFile, Guid performedById)
        {
            var existing = await _context.ApplicationDocuments
                .Include(d => d.DocumentType)
                .FirstOrDefaultAsync(d => d.Id == existingFileId);
            if (existing == null) throw new Exception("Existing file not found");

            // PROTECTION: Prevent replacement of legacy documents
            if (existing.IsLegacy || (!string.IsNullOrWhiteSpace(existing.FilePath) && 
                (existing.FileContent == null || existing.FileContent.Length == 0)))
            {
                throw new Exception("Cannot replace legacy documents. This file is from the old system and is read-only. " +
                    "Only documents stored in the new system can be replaced.");
            }

            if (newFile == null || newFile.Length == 0)
                throw new Exception("No file provided");

            // Validate file extension - only PDF allowed
            var allowedExtensions = _configuration.GetSection("FileStorage:AllowedExtensions").Get<string[]>() ?? new[] { ".pdf" };
            var fileExtension = Path.GetExtension(newFile.FileName).ToLowerInvariant();
            if (!allowedExtensions.Contains(fileExtension))
                throw new Exception($"File type {fileExtension} is not allowed. Only PDF files are permitted.");

            // Validate file size: use document-type-specific limit
            // Ensure DocumentType is loaded - if null, reload from database
            if (existing.DocumentType == null)
            {
                existing.DocumentType = await _context.DocumentTypes.FindAsync(existing.DocumentTypeId);
            }

            if (existing.DocumentType == null)
                throw new Exception("Document type configuration not found");

            int maxFileSizeKB = existing.DocumentType.MaxFileSizeKB;
            long maxFileSizeBytes = (long)maxFileSizeKB * 1024;
            if (newFile.Length > maxFileSizeBytes)
                throw new Exception($"File size exceeds maximum allowed size of {maxFileSizeKB}KB for {existing.DocumentType.Code}. Uploaded file is {newFile.Length / 1024}KB.");

            // Read file bytes for BLOB storage only
            byte[] fileBytes;
            using (var memoryStream = new MemoryStream())
            {
                await newFile.CopyToAsync(memoryStream);
                fileBytes = memoryStream.ToArray();
            }

            var newStoredFileName = GetStoredFileName(string.Empty, newFile.FileName);

            // Use execution strategy to handle transient failures within manual transactions
            var strategy = _context.Database.CreateExecutionStrategy();
            await strategy.ExecuteAsync(async () =>
            {
                using (var transaction = await _context.Database.BeginTransactionAsync())
                {
                    try
                    {
                        // Update db record with new BLOB only (no disk storage)
                        existing.FileName = newStoredFileName;
                        existing.FilePath = null;  // BLOB-only, no disk storage
                        existing.FileContent = fileBytes;  // Store BLOB in database
                        existing.FileType = newFile.ContentType;
                        existing.FileSize = newFile.Length;

                        // ===== Reset document review status for re-review =====
                        // When client replaces a rejected/revision-requested document,
                        // reset the review metadata so reviewer can review the new version
                        existing.ReviewStatus = "Pending";           // Reset to pending review
                        existing.ReviewComments = null;              // Clear old rejection/revision reason
                        existing.ReviewedBy = null;                  // Clear reviewer who rejected it
                        existing.ReviewedAt = null;                  // Clear old review timestamp
                        // ===== END REVIEW STATUS RESET =====

                        // Do NOT change application status on document replace
                        // Application status should only change when:
                        // 1. Reviewer explicitly updates via PUT /api/applications/{id}/status
                        // 2. Client explicitly submits via POST /api/applications/{id}/submit
                        // Document replacement only resets ReviewStatus to "Pending" for re-review

                        await _context.SaveChangesAsync();
                        await transaction.CommitAsync();
                    }
                    catch
                    {
                        await transaction.RollbackAsync();
                        throw;
                    }
                }
            });

            // Fetch the updated application status to return to frontend
            var updatedApp = await _context.Applications.FindAsync(existing.ApplicationId);
            var newApplicationStatus = updatedApp?.Status ?? string.Empty;

            return new FileUploadResponse
            {
                FileName = newStoredFileName,
                FilePath = null,  // BLOB-only replacement doesn't have disk path
                FileType = newFile.ContentType,
                FileSize = newFile.Length,
                IsBlobStored = true,
                IsLegacy = false,
                ApplicationStatus = newApplicationStatus  // Include updated status
            };
        }

        /// <summary>
        /// Merge all BLOB documents of an application into a single PDF file.
        /// - Only includes BLOB-stored documents (FileContent is not null)
        /// - Excludes legacy disk-stored documents
        /// - Documents are merged in order of CreatedAt (ascending)
        /// - Returns merged PDF as byte array
        /// - Optimized: Loads documents one-at-a-time to minimize memory usage
        /// </summary>
        public async Task<byte[]> MergeBlobDocumentsAsync(Guid applicationId)
        {
            // First, retrieve document metadata only (without FileContent) to validate documents exist and are PDFs
            // This is much faster and uses minimal memory
            var documentMetadata = await _context.ApplicationDocuments
                .AsNoTracking()
                .Where(d => d.ApplicationId == applicationId && 
                           d.FileContent != null && 
                           d.FileContent.Length > 0 &&
                           !d.IsLegacy)
                .OrderBy(d => d.CreatedAt)
                .Select(d => new { d.Id, d.FileName, d.FileType, d.CreatedAt })
                .ToListAsync();

            if (documentMetadata == null || documentMetadata.Count == 0)
                throw new Exception("No BLOB documents found for this application. Only documents stored in the new system can be downloaded as merged PDF.");

            // Validate all documents are PDFs (from metadata, so no large data transfer)
            var invalidDocs = documentMetadata.Where(d => d.FileType != "application/pdf").ToList();
            if (invalidDocs.Any())
                throw new Exception($"Cannot merge documents. Found {invalidDocs.Count} non-PDF document(s). Only PDF documents can be merged.");

            using (var outputStream = new MemoryStream())
            {
                PdfDocument pdfDocument = null;
                try
                {
                    // Initialize PDF writer and document for output
                    var pdfWriter = new PdfWriter(outputStream);
                    pdfDocument = new PdfDocument(pdfWriter);

                    // Process each document one-at-a-time to minimize memory usage
                    // Load FileContent only when needed, not upfront
                    int docIndex = 0;
                    foreach (var docMetadata in documentMetadata)
                    {
                        docIndex++;
                        try
                        {
                            // Load ONLY this document's FileContent (just-in-time loading)
                            // This ensures we only keep one document in memory at a time
                            var doc = await _context.ApplicationDocuments
                                .AsNoTracking()
                                .Where(d => d.Id == docMetadata.Id)
                                .Select(d => new { d.Id, d.FileName, d.FileContent, d.FileType })
                                .FirstOrDefaultAsync();

                            if (doc == null || doc.FileContent == null || doc.FileContent.Length == 0)
                                throw new Exception($"Document {docIndex} (ID: {docMetadata.Id}) could not be loaded or has no content");

                            // Create reader with better error handling
                            try
                            {
                                // Create stream with reset to beginning
                                var sourceStream = new MemoryStream(doc.FileContent);
                                sourceStream.Seek(0, SeekOrigin.Begin);

                                var sourcePdfReader = new PdfReader(sourceStream);
                                var sourcePdfDocument = new PdfDocument(sourcePdfReader);

                                try
                                {
                                    // Validate PDF has pages
                                    int pageCount = 0;
                                    try
                                    {
                                        pageCount = sourcePdfDocument.GetNumberOfPages();
                                    }
                                    catch (Exception pageEx)
                                    {
                                        throw new Exception($"Cannot read page count from document {docIndex} ('{doc.FileName}'): {pageEx.Message}", pageEx);
                                    }

                                    if (pageCount == 0)
                                        throw new Exception($"Document {docIndex} ('{doc.FileName}') has no pages");

                                    // Copy all pages from source document to merged document
                                    for (int i = 1; i <= pageCount; i++)
                                    {
                                        try
                                        {
                                            var page = sourcePdfDocument.GetPage(i);
                                            var copiedPage = page.CopyTo(pdfDocument);
                                            pdfDocument.AddPage(copiedPage);
                                        }
                                        catch (Exception pageEx)
                                        {
                                            throw new Exception($"Error copying page {i} from document {docIndex} ('{doc.FileName}'): {pageEx.Message}", pageEx);
                                        }
                                    }
                                }
                                finally
                                {
                                    sourcePdfDocument?.Close();
                                }
                            }
                            catch (Exception readerEx)
                            {
                                throw new Exception($"Error reading PDF from document {docIndex} ('{doc.FileName}'): {readerEx.Message}", readerEx);
                            }
                        }
                        catch (Exception docEx)
                        {
                            throw new Exception($"Error processing document {docIndex} ('{docMetadata.FileName}'): {docEx.Message}", docEx);
                        }
                    }

                    pdfDocument.Close();
                    return outputStream.ToArray();
                }
                catch (Exception ex)
                {
                    pdfDocument?.Close();

                    // Log the full exception details for debugging
                    _logger.LogError(ex, "PDF merge failed with exception. Message: {Message}, InnerException: {InnerMessage}, StackTrace: {StackTrace}",
                        ex.Message,
                        ex.InnerException?.Message ?? "N/A",
                        ex.StackTrace);

                    // Also include inner exception message in response if available
                    string detailedMessage = ex.Message;
                    if (ex.InnerException != null)
                    {
                        detailedMessage = $"{ex.Message} - Inner: {ex.InnerException.Message}";
                    }

                    throw new Exception($"Error merging PDF documents: {detailedMessage}", ex);
                }
            }
        }
    }
}