using MMGT.API.Services.Interfaces;
using MMGT.API.DTOs.Auth;
using MMGT.API.DTOs.User;
using MMGT.API.Data;
using MMGT.API.Helpers;
using Microsoft.EntityFrameworkCore;

namespace MMGT.API.Services
{
    public class AuthService : IAuthService
    {
        private readonly ApplicationDbContext _context;
        private readonly JwtHelper _jwtHelper;

        public AuthService(ApplicationDbContext context, JwtHelper jwtHelper)
        {
            _context = context;
            _jwtHelper = jwtHelper;
        }

        public async Task<LoginResponse> LoginAsync(LoginRequest request)
        {
            var user = await _context.Users
                .Include(u => u.PMICompany)
                .FirstOrDefaultAsync(u => u.Email == request.Email && u.IsActive);

            if (user == null || !BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
            {
                throw new Exception("Invalid email or password");
            }

            // Update last login
            user.LastLoginAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            // Generate token
            var token = _jwtHelper.GenerateToken(user);

            // Create response
            var response = new LoginResponse
            {
                Token = token,
                RefreshToken = Guid.NewGuid().ToString(), // Simple refresh token for now
                ExpiresAt = DateTime.UtcNow.AddMinutes(60),
                RequiresPasswordChange = user.IsFirstLogin,
                UserData = new UserDto
                {
                    Id = user.Id,
                    Email = user.Email,
                    FirstName = user.FirstName,
                    LastName = user.LastName,
                    Role = user.Role,
                    IsFirstLogin = user.IsFirstLogin,
                    IsActive = user.IsActive,
                    CreatedAt = user.CreatedAt,
                    LastLoginAt = user.LastLoginAt,
                    PMIEmail = user.PMICompany?.EmailAddress,
                    PMICompanyId = user.PMICompanyId,
                    PMICompanyName = user.PMICompany?.PmiName
                }
            };

            return response;
        }

        public async Task<bool> ChangePasswordAsync(Guid userId, ChangePasswordRequest request)
        {
            var user = await _context.Users.FindAsync(userId);
            if (user == null)
            {
                throw new Exception("User not found");
            }

            // Verify current password
            if (!BCrypt.Net.BCrypt.Verify(request.CurrentPassword, user.PasswordHash))
            {
                throw new Exception("Current password is incorrect");
            }

            // Hash new password
            user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.NewPassword);
            user.IsFirstLogin = false;
            user.UpdatedAt = DateTime.Now;

            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<bool> ValidateTokenAsync(string token)
        {
            var principal = _jwtHelper.ValidateToken(token);
            return principal != null;
        }
    }
}