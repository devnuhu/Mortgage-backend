using MMGT.API.DTOs.Auth;

namespace MMGT.API.Services.Interfaces
{
    public interface IAuthService
    {
        Task<MMGT.API.DTOs.Auth.LoginResponse> LoginAsync(MMGT.API.DTOs.Auth.LoginRequest request);
        Task<bool> ChangePasswordAsync(Guid userId, MMGT.API.DTOs.Auth.ChangePasswordRequest request);
        Task<bool> ValidateTokenAsync(string token);
    }
}