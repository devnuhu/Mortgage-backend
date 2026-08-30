using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MMGT.API.Models
{
    /// <summary>
    /// Defines escalation rules that trigger when reviewer performance falls below thresholds.
    /// Managed by LEAD_REVIEWER and SUPER_ADMIN.
    /// Used to automatically detect and escalate delayed assignments.
    /// </summary>
    public class EscalationRule
    {
        /// <summary>
        /// Primary key (GUID)
        /// </summary>
        public Guid Id { get; set; } = Guid.NewGuid();

        /// <summary>
        /// Rule name for identification (e.g., "High Priority Delay", "SLA Breach")
        /// </summary>
        [Required]
        [MaxLength(200)]
        public string RuleName { get; set; } = string.Empty;

        /// <summary>
        /// Description of what this rule does
        /// </summary>
        [MaxLength(500)]
        public string? Description { get; set; }

        /// <summary>
        /// Hours after assignment to trigger escalation (e.g., 24, 48, 72)
        /// </summary>
        [Required]
        public int ThresholdHours { get; set; }

        /// <summary>
        /// Application priority this rule applies to: "all", "high", "urgent"
        /// </summary>
        [Required]
        [MaxLength(50)]
        public string ApplicablePriority { get; set; } = "all";

        /// <summary>
        /// Action to take when escalation triggers: "notify", "reassign", "escalate_to_lead"
        /// </summary>
        [Required]
        [MaxLength(50)]
        public string EscalationAction { get; set; } = "notify"; // notify, reassign, escalate_to_lead

        /// <summary>
        /// Email addresses to notify when escalation triggers (semicolon-separated)
        /// </summary>
        [MaxLength(500)]
        public string? NotifyEmails { get; set; }

        /// <summary>
        /// Is this rule currently active?
        /// </summary>
        [Required]
        public bool IsActive { get; set; } = true;

        /// <summary>
        /// Who created this rule
        /// </summary>
        [Required]
        public Guid CreatedBy { get; set; }

        /// <summary>
        /// When rule was created
        /// </summary>
        [Required]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Who last modified this rule
        /// </summary>
        public Guid? ModifiedBy { get; set; }

        /// <summary>
        /// When rule was last modified
        /// </summary>
        public DateTime? ModifiedAt { get; set; }

        // Navigation properties
        [ForeignKey(nameof(CreatedBy))]
        public virtual User? CreatedByUser { get; set; }

        [ForeignKey(nameof(ModifiedBy))]
        public virtual User? ModifiedByUser { get; set; }

        [InverseProperty("EscalationRule")]
        public virtual ICollection<AssignmentEscalation> Escalations { get; set; } = new List<AssignmentEscalation>();
    }
}
