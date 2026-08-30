namespace MMGT.API.DTOs.Assignment
{
    public class AssignmentResponseDto
    {
        public Guid Id { get; set; }
        public Guid ApplicationId { get; set; }
        public Guid ReviewerId { get; set; }
        public Guid? AssignedBy { get; set; }
        public DateTime AssignedAt { get; set; }
        public bool IsActive { get; set; }
        public string? ReassignmentReason { get; set; }
    }
}