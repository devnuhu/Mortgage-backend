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
        
        /// <summary>Required when DocumentType is OTHER: the name of the document, for example "Utility bill".</summary>
        [MaxLength(150)]
        public string? DocumentName { get; set; }

        [Required]
        public IFormFile File { get; set; } = null!;
    }
}