using MMGT.API.DTOs.User;

namespace MMGT.API.Services.Interfaces
{
    public interface IUserService
    {
        Task<UserDto> GetUserByIdAsync(Guid id);
        Task<UserDto> CreateUserAsync(CreateUserRequest request);
        Task<bool> UpdateUserAsync(Guid id, object updateData);
        Task<bool> DeleteUserAsync(Guid id);
        Task<string?> ResetUserPasswordAsync(Guid userId);

        // New: regenerate & resend the one-time first-login setup link
        Task<bool> ResendFirstLoginLinkAsync(Guid userId);
    }
}