namespace MMGT.API.DTOs.Report
{
    public class ReportSummaryDto
    {
        public int TotalRecords { get; set; }
        public int SubmittedCount { get; set; }
        public int UnderReviewCount { get; set; }
        public int PendingCount { get; set; }
        public int RejectedCount { get; set; }
        public int ReturnedCount { get; set; }
        public int ApprovedCount { get; set; }
        public int CompletedCount { get; set; }
        public decimal TotalLoanAmount { get; set; }
        public decimal TotalPropertyValue { get; set; }
    }
}
