namespace MMGT.API.DTOs.Escalation
{
    public class AssignmentEscalationDto
    {
        public Guid Id { get; set; }
        public Guid ApplicationAssignmentId { get; set; }
        public Guid EscalationRuleId { get; set; }
        public DateTime EscalatedAt { get; set; }
        public string Status { get; set; } = string.Empty;
        public double HoursOverThreshold { get; set; }
        public Guid? ReassignedToReviewerId { get; set; }
        public string? ActionReason { get; set; }
        public DateTime? ResolvedAt { get; set; }
        public string? ResolutionComments { get; set; }
        public Guid? ResolvedBy { get; set; }
        public string? RuleName { get; set; }
        public Guid? ApplicationId { get; set; }
    }
}
