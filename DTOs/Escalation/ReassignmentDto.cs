namespace MMGT.API.DTOs.Escalation
{
    public class ReassignmentRequest
    {
        public Guid NewReviewerId { get; set; }
        public string Reason { get; set; } = string.Empty;
    }

    public class BulkReassignmentRequest
    {
        public List<Guid> AssignmentIds { get; set; } = new();
        public Guid NewReviewerId { get; set; }
        public string Reason { get; set; } = string.Empty;
    }

    public class ApplicationAssignmentDto
    {
        public Guid Id { get; set; }
        public Guid ApplicationId { get; set; }
        public string ApplicationNumber { get; set; } = string.Empty;
        public Guid ReviewerId { get; set; }
        public string ReviewerName { get; set; } = string.Empty;
        public string ReviewerEmail { get; set; } = string.Empty;
        public DateTime AssignedAt { get; set; }
        public bool IsActive { get; set; }
        public string? ReassignmentReason { get; set; }
        public Guid? AssignedBy { get; set; }
    }
}
