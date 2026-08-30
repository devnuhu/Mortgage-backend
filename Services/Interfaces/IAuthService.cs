using MMGT.API.DTOs.Auth;

namespace MMGT.API.Services.Interfaces
{
    public interface IAuthService
    {
        Task<MMGT.API.DTOs.Auth.LoginResponse> LoginAsync(MMGT.API.DTOs.Auth.LoginRequest request);
        Task<bool> ChangePasswordAsync(Guid userId, MMGT.API.DTOs.Auth.ChangePasswordRequest request);
        Task<bool> ValidateTokenAsync(string token);
        Task<AdminResetPasswordResponse> AdminResetPasswordAsync(Guid adminId, Guid targetUserId);

        // Role Login Access Control methods
        Task<(bool IsAllowed, string? DisabledMessage)> CheckRoleLoginAccessAsync(string role);
        Task<List<RoleLoginAccessDto>> GetAllRoleLoginAccessAsync();
        Task<RoleLoginAccessDto?> GetRoleLoginAccessAsync(string role);
        Task<RoleLoginAccessDto> UpdateRoleLoginAccessAsync(string role, UpdateRoleLoginAccessRequest request, Guid modifiedBy);

        // Time-based scheduling methods
        Task<RoleLoginAccessDto> ScheduleRoleAccessAsync(string role, ScheduleRoleAccessRequest request, Guid modifiedBy);
        Task<RoleLoginAccessDto> CancelScheduledAccessAsync(string role, Guid modifiedBy);
    }
}