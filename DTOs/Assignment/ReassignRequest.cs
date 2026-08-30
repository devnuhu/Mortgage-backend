namespace MMGT.API.DTOs.Assignment
{
    public class ReassignRequest
    {
        public Guid NewReviewerId { get; set; }
        public string Reason { get; set; } = string.Empty;
    }
}