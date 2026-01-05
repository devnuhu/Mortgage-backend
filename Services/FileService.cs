using MMGT.API.Services.Interfaces;
using MMGT.API.DTOs.File;
using MMGT.API.Data;
using MMGT.API.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using System.Linq;
using System.IO;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace MMGT.API.Services
{
    public class FileService : IFileService
    {
        private readonly ApplicationDbContext _context;
        private readonly IConfiguration _configuration;
        private readonly IWebHostEnvironment _environment;

        public FileService(ApplicationDbContext context, IConfiguration configuration, IWebHostEnvironment environment)
        {
            _context = context;
            _configuration = configuration;
            _environment = environment;
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

        public async Task<FileUploadResponse> UploadFileAsync(IFormFile file, Guid applicationId, string documentType, Guid performedById)
        {
            var application = await _context.Applications.FindAsync(applicationId);
            if (application == null)
                throw new Exception("Application not found");

            if (file == null || file.Length == 0)
                throw new Exception("No file provided");

            var maxFileSize = _configuration.GetValue<int>("FileStorage:MaxFileSizeInMB", 10) * 1024 * 1024;
            if (maxFileSize > 0 && file.Length > maxFileSize)
                throw new Exception($"File size exceeds maximum allowed size of {maxFileSize / (1024 * 1024)}MB");

            var allowedExtensions = _configuration.GetSection("FileStorage:AllowedExtensions").Get<string[]>() ?? new[] { ".pdf" };
            var fileExtension = Path.GetExtension(file.FileName).ToLowerInvariant();
            if (!allowedExtensions.Contains(fileExtension))
                throw new Exception($"File type {fileExtension} is not allowed. Allowed types: {string.Join(", ", allowedExtensions)}");

            var uploadPath = _configuration.GetValue<string>("FileStorage:UploadPath", "wwwroot/uploads");
            var contentRoot = _environment.ContentRootPath ?? string.Empty; // guard against null
            var fullUploadPath = Path.Combine(contentRoot, uploadPath ?? "wwwroot/uploads");
            Directory.CreateDirectory(fullUploadPath);

            var applicationPath = Path.Combine(fullUploadPath, applicationId.ToString());
            Directory.CreateDirectory(applicationPath);

            var storedFileName = GetStoredFileName(application.RsaPin ?? string.Empty, file.FileName);
            var filePath = Path.Combine(applicationPath, storedFileName);

            using (var transaction = await _context.Database.BeginTransactionAsync())
            {
                try
                {
                    using (var stream = new FileStream(filePath, FileMode.Create))
                    {
                        await file.CopyToAsync(stream);
                    }

                    var applicationDocument = new ApplicationDocument
                    {
                        Id = Guid.NewGuid(),
                        FileName = storedFileName,
                        FilePath = filePath,
                        FileType = file.ContentType,
                        DocumentType = documentType,
                        FileSize = file.Length,
                        ApplicationId = applicationId,
                        CreatedAt = DateTime.UtcNow
                    };

                    _context.ApplicationDocuments.Add(applicationDocument);

                    if (application.Status == "rejected")
                    {
                        application.Status = "resubmitted";
                        application.UpdatedAt = DateTime.UtcNow;

                        var reviewHistory = new ApplicationReviewHistory
                        {
                            Id = Guid.NewGuid(),
                            ApplicationId = applicationId,
                            ReviewedById = performedById,
                            Status = "resubmitted",
                            Comments = "Client re-submitted application after replacing/adding documents",
                            CreatedAt = DateTime.UtcNow
                        };

                        _context.ApplicationReviewHistory.Add(reviewHistory);
                    }

                    await _context.SaveChangesAsync();
                    await transaction.CommitAsync();
                }
                catch
                {
                    await transaction.RollbackAsync();
                    throw;
                }
            }

            return new FileUploadResponse
            {
                FileName = storedFileName,
                FilePath = filePath,
                FileType = file.ContentType,
                FileSize = file.Length
            };
        }

        // Get a single file metadata
        public async Task<FileUploadResponse> GetFileAsync(Guid fileId)
        {
            var doc = await _context.ApplicationDocuments.FirstOrDefaultAsync(d => d.Id == fileId);
            if (doc == null)
                throw new Exception("File not found");

            return new FileUploadResponse
            {
                Id = doc.Id,
                ApplicationId = doc.ApplicationId,
                FileName = doc.FileName,
                FilePath = doc.FilePath,
                FileType = doc.FileType,
                DocumentType = doc.DocumentType,
                FileSize = doc.FileSize
            };
        }

        // Get files by application
        public async Task<List<FileUploadResponse>> GetFilesByApplicationAsync(Guid applicationId)
        {
            var docs = await _context.ApplicationDocuments
                .Where(d => d.ApplicationId == applicationId)
                .ToListAsync();

            return docs.Select(doc => new FileUploadResponse
            {
                Id = doc.Id,
                ApplicationId = doc.ApplicationId,
                FileName = doc.FileName,
                FilePath = doc.FilePath,
                FileType = doc.FileType,
                DocumentType = doc.DocumentType,
                FileSize = doc.FileSize
            }).ToList();
        }

        // Delete a file
        public async Task<bool> DeleteFileAsync(Guid fileId, Guid performedById)
        {
            var doc = await _context.ApplicationDocuments.FirstOrDefaultAsync(d => d.Id == fileId);
            if (doc == null) return false;

            // remove physical file if exists
            try
            {
                if (!string.IsNullOrWhiteSpace(doc.FilePath) && File.Exists(doc.FilePath))
                {
                    File.Delete(doc.FilePath);
                }
            }
            catch
            {
                // ignore file delete errors but continue to remove db record
            }

            _context.ApplicationDocuments.Remove(doc);
            await _context.SaveChangesAsync();
            return true;
        }

        // Download file bytes
        public async Task<byte[]> DownloadFileAsync(Guid fileId)
        {
            var doc = await _context.ApplicationDocuments.FirstOrDefaultAsync(d => d.Id == fileId);
            if (doc == null) throw new Exception("File not found");

            if (string.IsNullOrWhiteSpace(doc.FilePath) || !File.Exists(doc.FilePath))
                throw new Exception("File not found on disk");

            return await File.ReadAllBytesAsync(doc.FilePath);
        }

        // Replace an existing file atomically
        public async Task<FileUploadResponse> ReplaceFileAsync(Guid existingFileId, IFormFile newFile, Guid performedById)
        {
            var existing = await _context.ApplicationDocuments.FirstOrDefaultAsync(d => d.Id == existingFileId);
            if (existing == null) throw new Exception("Existing file not found");

            if (newFile == null || newFile.Length == 0)
                throw new Exception("No file provided");

            var maxFileSize = _configuration.GetValue<int>("FileStorage:MaxFileSizeInMB", 10) * 1024 * 1024;
            if (maxFileSize > 0 && newFile.Length > maxFileSize)
                throw new Exception($"File size exceeds maximum allowed size of {maxFileSize / (1024 * 1024)}MB");

            var allowedExtensions = _configuration.GetSection("FileStorage:AllowedExtensions").Get<string[]>() ?? new[] { ".pdf" };
            var fileExtension = Path.GetExtension(newFile.FileName).ToLowerInvariant();
            if (!allowedExtensions.Contains(fileExtension))
                throw new Exception($"File type {fileExtension} is not allowed. Allowed types: {string.Join(", ", allowedExtensions)}");

            var uploadPath = _configuration.GetValue<string>("FileStorage:UploadPath", "wwwroot/uploads");
            var contentRoot = _environment.ContentRootPath ?? string.Empty; // guard against null
            var fullUploadPath = Path.Combine(contentRoot, uploadPath ?? "wwwroot/uploads");
            Directory.CreateDirectory(fullUploadPath);

            var applicationPath = Path.Combine(fullUploadPath, existing.ApplicationId.ToString());
            Directory.CreateDirectory(applicationPath);

            var newStoredFileName = GetStoredFileName(string.Empty, newFile.FileName);
            var newFilePath = Path.Combine(applicationPath, newStoredFileName);

            using (var transaction = await _context.Database.BeginTransactionAsync())
            {
                try
                {
                    // save new file
                    using (var stream = new FileStream(newFilePath, FileMode.Create))
                    {
                        await newFile.CopyToAsync(stream);
                    }

                    // delete old physical file
                    try
                    {
                        if (!string.IsNullOrWhiteSpace(existing.FilePath) && File.Exists(existing.FilePath))
                        {
                            File.Delete(existing.FilePath);
                        }
                    }
                    catch
                    {
                        // ignore
                    }

                    // update db record
                    existing.FileName = newStoredFileName;
                    existing.FilePath = newFilePath;
                    existing.FileType = newFile.ContentType;
                    existing.FileSize = newFile.Length;

                    // if application was rejected mark resubmitted
                    var application = await _context.Applications.FindAsync(existing.ApplicationId);
                    if (application != null && application.Status == "rejected")
                    {
                        application.Status = "resubmitted";
                        application.UpdatedAt = DateTime.UtcNow;

                        var reviewHistory = new ApplicationReviewHistory
                        {
                            Id = Guid.NewGuid(),
                            ApplicationId = application.Id,
                            ReviewedById = performedById,
                            Status = "resubmitted",
                            Comments = "Client re-submitted application after replacing document",
                            CreatedAt = DateTime.UtcNow
                        };

                        _context.ApplicationReviewHistory.Add(reviewHistory);
                    }

                    await _context.SaveChangesAsync();
                    await transaction.CommitAsync();
                }
                catch
                {
                    await transaction.RollbackAsync();
                    throw;
                }
            }

            return new FileUploadResponse
            {
                FileName = newStoredFileName,
                FilePath = newFilePath,
                FileType = newFile.ContentType,
                FileSize = newFile.Length
            };
        }
    }
}