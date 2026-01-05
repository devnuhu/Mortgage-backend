using System.ComponentModel.DataAnnotations;

namespace MMGT.API.DTOs.File
{
    public class FileUploadRequest
    {
        [Required]
        public Guid ApplicationId { get; set; }

        [Required]
        [MaxLength(100)]
        public string DocumentType { get; set; } = string.Empty; // identity_document, income_proof, property_document, etc.
    }
}