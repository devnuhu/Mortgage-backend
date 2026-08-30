namespace MMGT.API.DTOs.Escalation
{
    public class EscalationRuleDto
    {
        public Guid Id { get; set; }
        public string RuleName { get; set; } = string.Empty;
        public string? Description { get; set; }
        public int ThresholdHours { get; set; }
        public string ApplicablePriority { get; set; } = string.Empty;
        public string EscalationAction { get; set; } = string.Empty;
        public string? NotifyEmails { get; set; }
        public bool IsActive { get; set; }
        public DateTime CreatedAt { get; set; }
        public Guid CreatedBy { get; set; }
        public DateTime? ModifiedAt { get; set; }
        public Guid? ModifiedBy { get; set; }
    }

    public class CreateEscalationRuleRequest
    {
        public string RuleName { get; set; } = string.Empty;
        public string? Description { get; set; }
        public int ThresholdHours { get; set; }
        public string ApplicablePriority { get; set; } = "all";
        public string EscalationAction { get; set; } = "notify"; // notify, reassign, escalate_to_lead
        public string? NotifyEmails { get; set; }
    }

    public class UpdateEscalationRuleRequest
    {
        public string? RuleName { get; set; }
        public string? Description { get; set; }
        public int? ThresholdHours { get; set; }
        public string? ApplicablePriority { get; set; }
        public string? EscalationAction { get; set; }
        public string? NotifyEmails { get; set; }
    }
}
