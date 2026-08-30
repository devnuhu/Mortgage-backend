// Services/Interfaces/IApplicationService.cs
using MMGT.API.DTOs.Application;
using MMGT.API.DTOs;
using MMGT.API.DTOs.Assignment;

namespace MMGT.API.Services.Interfaces
{
    public interface IApplicationService
    {
        Task<List<ApplicationDto>> GetAllApplicationsAsync();
        Task<PaginatedResponse<ApplicationDto>> GetAllApplicationsPagedAsync(PaginationRequest pagination);
        Task<List<ApplicationDto>> GetApplicationsByUserAsync(Guid userId);
        Task<PaginatedResponse<ApplicationDto>> GetApplicationsByUserPagedAsync(Guid userId, PaginationRequest pagination, string? searchTerm = null);
        Task<ClientApplicationSummaryDto> GetApplicationsSummaryByUserAsync(Guid userId);
        Task<PaginatedResponse<ApplicationDetailsDto>> GetApplicationsByUserRejectedPagedAsync(Guid userId, PaginationRequest pagination);
        Task<List<ApplicationDto>> GetApplicationsByPMIAsync(Guid pmiCompanyId);
        Task<PaginatedResponse<ApplicationDto>> GetApplicationsByPMIPagedAsync(Guid pmiCompanyId, PaginationRequest pagination);
        Task<ApplicationDto> GetApplicationByIdAsync(Guid id);
        Task<ApplicationDto> CreateApplicationAsync(Guid userId, CreateApplicationRequest request);
        Task<ApplicationDto> UpdateApplicationStatusAsync(Guid id, Guid reviewerId, UpdateApplicationStatusRequest request);
        Task<bool> SubmitApplicationAsync(Guid id);
        Task<bool> DeleteApplicationAsync(Guid id);
        Task<List<ReviewHistoryDto>> GetApplicationReviewHistoryAsync(Guid applicationId);
        Task<PaginatedResponse<ReviewHistoryDto>> GetApplicationReviewHistoryPagedAsync(Guid applicationId, PaginationRequest pagination);
        Task<List<AssignmentHistoryDto>> GetApplicationAssignmentHistoryAsync(Guid applicationId);
        Task<PaginatedResponse<AssignmentHistoryDto>> GetApplicationAssignmentHistoryPagedAsync(Guid applicationId, PaginationRequest pagination);
        Task<List<ApplicationDto>> SearchApplicationsAsync(string searchTerm);

        // Allow client users to update their application details when application is in editable status
        Task<ApplicationDto> UpdateApplicationAsync(Guid id, Guid userId, UpdateApplicationRequest request);

        // SERVICE_MANAGER search and details endpoints
        Task<PaginatedResponse<ApplicationSearchDto>> SearchApplicationsAdvancedAsync(
            string? rsaPin = null,
            string? borrowerName = null,
            DateTime? submittedFromDate = null,
            DateTime? submittedToDate = null,
            int pageNumber = 1,
            int pageSize = 50);

        Task<ApplicationDetailsDto> GetApplicationDetailsAsync(Guid applicationId);

        // ===== NEW: RSA PIN validation helpers used by controller/service =====
        Task<bool> CheckRsaPinExistsAsync(string rsaPin);
        Task<ApplicationDto?> GetApplicationByRsaPinAsync(string rsaPin);
    }
}