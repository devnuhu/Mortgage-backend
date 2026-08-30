using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MMGT.API.Models
{
    /// <summary>
    /// Tracks escalations triggered for assignments that exceed turnaround time thresholds.
    /// Immutable audit trail of all escalations and their actions.
    /// </summary>
    public class AssignmentEscalation
    {
        /// <summary>
        /// Primary key (GUID)
        /// </summary>
        public Guid Id { get; set; } = Guid.NewGuid();

        /// <summary>
        /// The application assignment that triggered the escalation
        /// </summary>
        [Required]
        public Guid ApplicationAssignmentId { get; set; }

        /// <summary>
        /// The escalation rule that triggered
        /// </summary>
        [Required]
        public Guid EscalationRuleId { get; set; }

        /// <summary>
        /// When the escalation was triggered
        /// </summary>
        [Required]
        public DateTime EscalatedAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Status: "triggered", "notified", "reassigned", "escalated_to_lead", "resolved"
        /// </summary>
        [Required]
        [MaxLength(50)]
        public string Status { get; set; } = "triggered";

        /// <summary>
        /// How many hours the assignment exceeded the threshold
        /// </summary>
        [Required]
        public double HoursOverThreshold { get; set; } = 0.0;

        /// <summary>
        /// If action was reassignment, who was reassigned to
        /// </summary>
        public Guid? ReassignedToReviewerId { get; set; }

        /// <summary>
        /// Reason for the escalation action
        /// </summary>
        [MaxLength(500)]
        public string? ActionReason { get; set; }

        /// <summary>
        /// When escalation was resolved/closed (null if still active)
        /// </summary>
        public DateTime? ResolvedAt { get; set; }

        /// <summary>
        /// Comments on escalation resolution
        /// </summary>
        [MaxLength(1000)]
        public string? ResolutionComments { get; set; }

        /// <summary>
        /// Who resolved this escalation
        /// </summary>
        public Guid? ResolvedBy { get; set; }

        // Navigation properties
        [ForeignKey(nameof(ApplicationAssignmentId))]
        public virtual ApplicationAssignment? ApplicationAssignment { get; set; }

        [ForeignKey(nameof(EscalationRuleId))]
        public virtual EscalationRule? EscalationRule { get; set; }

        [ForeignKey(nameof(ReassignedToReviewerId))]
        public virtual User? ReassignedToReviewer { get; set; }

        [ForeignKey(nameof(ResolvedBy))]
        public virtual User? ResolvedByUser { get; set; }
    }
}
