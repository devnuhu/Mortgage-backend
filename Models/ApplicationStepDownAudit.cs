namespace MMGT.API.Models
{
    /// <summary>Append-only history of every step-down action. Rows are never updated or deleted.</summary>
    public class ApplicationStepDownAudit
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public Guid RequestId { get; set; }
        public Guid ApplicationId { get; set; }
        public string Action { get; set; } = string.Empty;
        public string? FromStatus { get; set; }
        public string? ToStatus { get; set; }
        public Guid PerformedById { get; set; }
        public string PerformedByName { get; set; } = string.Empty;
        public string PerformedByRole { get; set; } = string.Empty;
        public DateTime PerformedAt { get; set; } = DateTime.UtcNow;
        public string? Comment { get; set; }
        public string? Details { get; set; }
    }
}
