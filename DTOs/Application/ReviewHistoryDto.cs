namespace MMGT.API.DTOs.Application
{
    public class ReviewHistoryDto
    {
        public Guid Id { get; set; }
        public string Status { get; set; } = string.Empty;
        public string? Comments { get; set; }
        public DateTime CreatedAt { get; set; }
        public Guid ReviewedById { get; set; }
        public string ReviewedByName { get; set; } = string.Empty;
        public string ReviewedByEmail { get; set; } = string.Empty;
    }
}