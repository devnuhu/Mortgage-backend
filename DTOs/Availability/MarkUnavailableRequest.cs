namespace MMGT.API.DTOs.Availability
{
    public class MarkUnavailableRequest
    {
        public string Reason { get; set; } = string.Empty;
        public DateTime? EndDate { get; set; }
    }

    public class MarkOnLeaveRequest
    {
        public DateTime EndDate { get; set; }
        public string Reason { get; set; } = string.Empty;
    }
}
