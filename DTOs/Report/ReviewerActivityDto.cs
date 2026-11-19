namespace MMGT.API.DTOs.Report
{
    public class ReviewerActivityRowDto
    {
        public Guid ReviewerId { get; set; }
        public string ReviewerName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Role { get; set; } = string.Empty;

        // Workload today
        public int OpenNow { get; set; }
        public int OverdueNow { get; set; }
        public double? OldestOpenDays { get; set; }

        // Activity in the selected period
        public int ReceivedInPeriod { get; set; }
        public int DecisionsInPeriod { get; set; }
        public int Approved { get; set; }
        public int Rejected { get; set; }
        public int Returned { get; set; }
        public double? ReturnedPercent { get; set; }

        // Turnaround from the reviewer's own assignment to their own decision
        public int TurnaroundSampleSize { get; set; }
        public double? AverageTurnaroundDays { get; set; }
        public double? MedianTurnaroundDays { get; set; }
        public double? WithinTargetPercent { get; set; }
    }

    public class ReviewerActivityReportDto
    {
        public DateTime GeneratedAtUtc { get; set; }
        public DateTime? PeriodFrom { get; set; }
        public DateTime? PeriodTo { get; set; }
        public double TargetDays { get; set; }
        public List<ReviewerActivityRowDto> Reviewers { get; set; } = new();
        public ReviewerActivityRowDto Team { get; set; } = new();
        public List<string> Definitions { get; set; } = new();
    }
}