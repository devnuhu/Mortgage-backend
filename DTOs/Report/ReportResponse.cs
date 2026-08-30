using MMGT.API.DTOs.Application;

namespace MMGT.API.DTOs.Report
{
    public class ReportResponse<T>
    {
        public List<T> Data { get; set; } = new List<T>();
        public int TotalCount { get; set; }
        public int PageNumber { get; set; }
        public int PageSize { get; set; }
        public int TotalPages { get; set; }
        public bool HasNextPage { get; set; }
        public bool HasPreviousPage { get; set; }
        public ReportSummaryDto? Summary { get; set; }
    }
}
