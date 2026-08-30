using MMGT.API.DTOs.Application;
using MMGT.API.DTOs.Report;

namespace MMGT.API.Services.Interfaces
{
    public interface IReportService
    {
        Task<ReportResponse<ApplicationReportDto>> GenerateApplicationReportAsync(ReportFilterRequest filter);
        Task<ReportResponse<ApplicationDto>> GenerateApplicationReportFullAsync(ReportFilterRequest filter);
        Task<ReportSummaryDto> GetReportSummaryAsync(ReportFilterRequest filter);
        Task<byte[]> ExportApplicationsToExcelAsync(ReportFilterRequest filter);
    }
}
