using MMGT.API.DTOs.File;
using Microsoft.AspNetCore.Http;

namespace MMGT.API.Services.Interfaces
{
    public interface IFileService
    {
        Task<FileUploadResponse> UploadFileAsync(IFormFile file, Guid applicationId, string documentType, Guid performedById);
        Task<FileUploadResponse> GetFileAsync(Guid fileId);
        Task<List<FileUploadResponse>> GetFilesByApplicationAsync(Guid applicationId);
        Task<bool> DeleteFileAsync(Guid fileId, Guid performedById);
        Task<byte[]> DownloadFileAsync(Guid fileId);

        // Atomically replace an existing file with a new upload
        Task<FileUploadResponse> ReplaceFileAsync(Guid existingFileId, IFormFile newFile, Guid performedById);

        // Merge all BLOB documents for an application into a single PDF
        Task<byte[]> MergeBlobDocumentsAsync(Guid applicationId);
    }
}