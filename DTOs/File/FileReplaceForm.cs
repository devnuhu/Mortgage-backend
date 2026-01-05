using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;

namespace MMGT.API.DTOs.File
{
    public class FileReplaceForm
    {
        [Required]
        public Guid ExistingFileId { get; set; }

        [Required]
        public IFormFile File { get; set; } = null!;
    }
}
