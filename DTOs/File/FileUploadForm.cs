using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;

namespace MMGT.API.DTOs.File
{
    public class FileUploadForm
    {
        [Required]
        public Guid ApplicationId { get; set; }

        [Required, MaxLength(100)]
        public string DocumentType { get; set; } = string.Empty;

        [Required]
        public IFormFile File { get; set; } = null!;
    }
}