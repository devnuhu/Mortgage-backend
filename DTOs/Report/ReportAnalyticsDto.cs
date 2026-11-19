namespace MMGT.API.DTOs.Report
{
    public class StatusCountsDto
    {
        public int Total { get; set; }
        public int Submitted { get; set; }
        public int UnderReview { get; set; }
        public int Pending { get; set; }
        public int Approved { get; set; }
        public int Rejected { get; set; }
        public int Returned { get; set; }
        public int Completed { get; set; }
    }

    public class PmiBreakdownRowDto : StatusCountsDto
    {
        public string PmiCompany { get; set; } = string.Empty;
    }

    public class ReviewerBreakdownRowDto : StatusCountsDto
    {
        public Guid ReviewerId { get; set; }
        public string ReviewerName { get; set; } = string.Empty;
    }

    /// <summary>Applications submitted in the month, grouped by their current outcome.</summary>
    public class MonthlyTrendRowDto
    {
        public string Month { get; set; } = string.Empty; // yyyy-MM
        public int Submitted { get; set; }
        public int Approved { get; set; }
        public int Rejected { get; set; }
        public int Returned { get; set; }
        public int Created { get; set; }
        public int Awaiting { get; set; }
        public int Other { get; set; }
    }

    /// <summary>Days from submission to the decision (approved, rejected or returned).</summary>
    public class TurnaroundStatsDto
    {
        public int DecidedCount { get; set; }
        public double? AverageDays { get; set; }
        public double? ApprovedAverageDays { get; set; }
        public double? RejectedAverageDays { get; set; }
        public double? ReturnedAverageDays { get; set; }
    }

    public class ReportAnalyticsDto
    {
        public List<PmiBreakdownRowDto> ByPmi { get; set; } = new();
        public List<ReviewerBreakdownRowDto> ByReviewer { get; set; } = new();
        public List<MonthlyTrendRowDto> ByMonth { get; set; } = new();
        public TurnaroundStatsDto Turnaround { get; set; } = new();
    }
}