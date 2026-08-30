namespace MMGT.API.DTOs.Document
{
    public class DocumentTypeCreateRequest
    {
        public string Code { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public bool IsMandatory { get; set; } = true;
        public int DisplayOrder { get; set; } = 0;
        public int MaxFileSizeKB { get; set; } = 500;
    }
}