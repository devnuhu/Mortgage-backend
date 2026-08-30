using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MMGT.API.Models
{
    /// <summary>
    /// Tracks reviewer assignments for applications.
    /// Auto-assignment happens when application is submitted.
    /// Lead reviewers can manually reassign for absences/overload.
    /// Assignment records are immutable (never deleted) for audit trail.
    /// </summary>
    public class ApplicationAssignment
    {
        /// <summary>
        /// Primary key (GUID)
        /// </summary>
        public Guid Id { get; set; } = Guid.NewGuid();

        /// <summary>
        /// Application being assigned
        /// </summary>
        [Required]
        public Guid ApplicationId { get; set; }

        /// <summary>
        /// Reviewer assigned to this application
        /// </summary>
        [Required]
        public Guid ReviewerId { get; set; }

        /// <summary>
        /// Who assigned this (null = System auto-assignment, User ID = Lead Reviewer manual assignment)
        /// </summary>
        public Guid? AssignedBy { get; set; }

        /// <summary>
        /// When assignment occurred (UTC)
        /// </summary>
        [Required]
        public DateTime AssignedAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Reason for reassignment (mandatory if manually reassigned)
        /// e.g., "Reviewer on leave", "Workload balancing", "Skill-based"
        /// </summary>
        [MaxLength(500)]
        public string? ReassignmentReason { get; set; }

        /// <summary>
        /// Is this the current active assignment (true) or historical (false)?
        /// Only one assignment per application can be IsActive = true
        /// </summary>
        [Required]
        public bool IsActive { get; set; } = true;

        // Navigation properties
        [ForeignKey(nameof(ApplicationId))]
        public virtual Application? Application { get; set; }

        [ForeignKey(nameof(ReviewerId))]
        public virtual User? Reviewer { get; set; }

        [ForeignKey(nameof(AssignedBy))]
        public virtual User? AssignedByUser { get; set; }
    }
}
