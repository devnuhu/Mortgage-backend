using MMGT.API.DTOs.Application;

namespace MMGT.API.Services.Interfaces
{
    public interface IApplicationService
    {
        Task<List<ApplicationDto>> GetAllApplicationsAsync();
        Task<List<ApplicationDto>> GetApplicationsByUserAsync(Guid userId);
        Task<List<ApplicationDto>> GetApplicationsByPMIAsync(Guid pmiCompanyId);
        Task<ApplicationDto> GetApplicationByIdAsync(Guid id);
        Task<ApplicationDto> CreateApplicationAsync(Guid userId, CreateApplicationRequest request);
        Task<ApplicationDto> UpdateApplicationStatusAsync(Guid id, Guid reviewerId, UpdateApplicationStatusRequest request);
        Task<bool> SubmitApplicationAsync(Guid id);
        Task<bool> DeleteApplicationAsync(Guid id);
        Task<List<ReviewHistoryDto>> GetApplicationReviewHistoryAsync(Guid applicationId);

        // Allow client users to update their application details when application is in editable status
        Task<ApplicationDto> UpdateApplicationAsync(Guid id, Guid userId, UpdateApplicationRequest request);
    }
}