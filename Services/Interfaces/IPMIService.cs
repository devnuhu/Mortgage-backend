using MMGT.API.DTOs.PMI;
using MMGT.API.DTOs;

namespace MMGT.API.Services.Interfaces
{
    public interface IPMIService
    {
        Task<List<PMICompanyDto>> GetAllPMICompaniesAsync();
        Task<PaginatedResponse<PMICompanyDto>> GetAllPMICompaniesPagedAsync(PaginationRequest pagination);
        Task<PMICompanyDto> GetPMICompanyByIdAsync(Guid id);
        Task<PMICompanyDto> CreatePMICompanyAsync(CreatePMIRequest request);
        Task<PMICompanyDto> UpdatePMICompanyAsync(Guid id, UpdatePMIRequest request);
        Task<bool> DeletePMICompanyAsync(Guid id);
        Task<bool> UpdatePMIStatusAsync(Guid id, string status);
    }
}
