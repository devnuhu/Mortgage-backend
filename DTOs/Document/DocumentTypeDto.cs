namespace MMGT.API.DTOs.Document
{
    public class DocumentTypeDto
    {
        public Guid Id { get; set; }
        public string Code { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public bool IsMandatory { get; set; }
        public int DisplayOrder { get; set; }
        public int MaxFileSizeKB { get; set; }
        public bool IsActive { get; set; }
        public DateTime CreatedAt { get; set; }
        public Guid CreatedBy { get; set; }
        public DateTime? ModifiedAt { get; set; }
        public Guid? ModifiedBy { get; set; }
    }
}