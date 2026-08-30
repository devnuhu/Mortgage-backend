using MMGT.API.Services.Interfaces;
using MMGT.API.DTOs.Auth;
using MMGT.API.DTOs.User;
using MMGT.API.Data;
using MMGT.API.Helpers;
using MMGT.API.Exceptions;
using MMGT.API.Models;
using Microsoft.EntityFrameworkCore;
using System.Text;

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
            if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Password))
            {
                throw new ValidationException("Email and password are required");
            }

            var user = await _context.Users
                .Include(u => u.PMICompany)
                .FirstOrDefaultAsync(u => u.Email == request.Email && u.IsActive);

            if (user == null)
            {
                throw new AuthenticationException("No account found with this email address. Please check and try again.");
            }

            if (!BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
            {
                throw new AuthenticationException("Incorrect password. Please try again or use the forgot password option.");
            }

            // Check if login is enabled for this role
            var (isLoginAllowed, disabledMessage) = await CheckRoleLoginAccessAsync(user.Role);
            if (!isLoginAllowed)
            {
                throw new AuthenticationException(disabledMessage ?? "Login access is currently disabled for your role. Please contact administrator for more information.");
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
                throw new ResourceNotFoundException("User", userId.ToString());
            }

            // Verify current password
            if (!BCrypt.Net.BCrypt.Verify(request.CurrentPassword, user.PasswordHash))
            {
                throw new ValidationException("Current password is incorrect. Please try again.");
            }

            // Validate new password is different
            if (request.CurrentPassword == request.NewPassword)
            {
                throw new ValidationException("New password must be different from current password.");
            }

            // Hash new password
            user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.NewPassword);
            user.IsFirstLogin = false;
            user.UpdatedAt = DateTime.UtcNow;

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                throw new DatabaseException($"Failed to update password: {ex.Message}");
            }

            return true;
        }

        public async Task<bool> ValidateTokenAsync(string token)
        {
            var principal = _jwtHelper.ValidateToken(token);
            return principal != null;
        }

        public async Task<AdminResetPasswordResponse> AdminResetPasswordAsync(Guid adminId, Guid targetUserId)
        {
            // Verify admin exists and has proper role
            var admin = await _context.Users.FindAsync(adminId);
            if (admin == null)
            {
                throw new ResourceNotFoundException("Admin user", adminId.ToString());
            }

            if (admin.Role != "SUPER_ADMIN")
            {
                throw new AuthorizationException("Only administrators with SUPER_ADMIN role can reset user passwords. Your current role does not have this permission.");
            }

            // Verify target user exists
            var targetUser = await _context.Users.FindAsync(targetUserId);
            if (targetUser == null)
            {
                throw new ResourceNotFoundException("User", targetUserId.ToString());
            }

            try
            {
                // Generate temporary password (12 characters with mix of upper, lower, numbers, special)
                var temporaryPassword = GenerateTemporaryPassword();

                // Hash and set the temporary password
                targetUser.PasswordHash = BCrypt.Net.BCrypt.HashPassword(temporaryPassword);
                targetUser.IsFirstLogin = true; // Force first login to change password
                targetUser.UpdatedAt = DateTime.UtcNow;

                await _context.SaveChangesAsync();

                return new AdminResetPasswordResponse
                {
                    UserId = targetUser.Id,
                    UserEmail = targetUser.Email,
                    UserName = $"{targetUser.FirstName} {targetUser.LastName}",
                    TemporaryPassword = temporaryPassword,
                    Message = $"Password reset successful for {targetUser.FirstName} {targetUser.LastName}. Share the temporary password with the user. They will be required to change it on first login."
                };
            }
            catch (Exception ex)
            {
                throw new DatabaseException($"Failed to reset password: {ex.Message}");
            }
        }

        private string GenerateTemporaryPassword()
        {
            const string uppercaseChars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";
            const string lowercaseChars = "abcdefghijklmnopqrstuvwxyz";
            const string digitChars = "0123456789";
            const string specialChars = "!@#$%^&*";

            var random = new Random();
            var password = new StringBuilder();

            // Ensure at least one of each type
            password.Append(uppercaseChars[random.Next(uppercaseChars.Length)]);
            password.Append(lowercaseChars[random.Next(lowercaseChars.Length)]);
            password.Append(digitChars[random.Next(digitChars.Length)]);
            password.Append(specialChars[random.Next(specialChars.Length)]);

            // Fill the rest randomly
            var allChars = uppercaseChars + lowercaseChars + digitChars + specialChars;
            for (int i = 4; i < 12; i++)
            {
                password.Append(allChars[random.Next(allChars.Length)]);
            }

            // Shuffle the password
            var passwordArray = password.ToString().ToCharArray();
            for (int i = passwordArray.Length - 1; i > 0; i--)
            {
                int randomIndex = random.Next(i + 1);
                (passwordArray[i], passwordArray[randomIndex]) = (passwordArray[randomIndex], passwordArray[i]);
            }

            return new string(passwordArray);
        }

        /// <summary>
        /// Checks if login is allowed for the given role
        /// </summary>
        /// <param name="role">The user role to check (e.g., "CLIENT", "SERVICE_MANAGER")</param>
        /// <returns>
        /// A tuple containing:
        /// - Item1: bool indicating if login is allowed (true) or denied (false)
        /// - Item2: display message (null if allowed, message if denied)
        /// </returns>
        public async Task<(bool IsAllowed, string? DisabledMessage)> CheckRoleLoginAccessAsync(string role)
        {
            var roleAccess = await _context.RoleLoginAccessControls
                .FirstOrDefaultAsync(r => r.Role == role);

            // If no configuration exists for this role, allow login by default
            if (roleAccess == null)
            {
                return (true, null);
            }

            var utcNow = DateTime.UtcNow;

            // Check if there's an active schedule
            if (roleAccess.IsScheduled && 
                roleAccess.ScheduledStartTime.HasValue && 
                roleAccess.ScheduledEndTime.HasValue)
            {
                // If we're within the scheduled disable window, deny access
                if (utcNow >= roleAccess.ScheduledStartTime && utcNow <= roleAccess.ScheduledEndTime)
                {
                    return (false, roleAccess.DisabledMessage);
                }

                // If the schedule has expired, auto-clear it and re-evaluate
                if (utcNow > roleAccess.ScheduledEndTime)
                {
                    roleAccess.IsScheduled = false;
                    roleAccess.ScheduledStartTime = null;
                    roleAccess.ScheduledEndTime = null;
                    roleAccess.ScheduledReason = null;
                    roleAccess.LastModifiedAt = DateTime.UtcNow;

                    try
                    {
                        _context.RoleLoginAccessControls.Update(roleAccess);
                        await _context.SaveChangesAsync();
                    }
                    catch
                    {
                        // If update fails, just continue with the check (don't block login)
                    }

                    // After clearing schedule, check the base IsLoginEnabled flag
                    if (!roleAccess.IsLoginEnabled)
                    {
                        return (false, roleAccess.DisabledMessage);
                    }
                    return (true, null);
                }
            }

            // Check base login enabled flag
            if (!roleAccess.IsLoginEnabled)
            {
                return (false, roleAccess.DisabledMessage);
            }

            return (true, null);
        }

        /// <summary>
        /// Gets all role login access control settings
        /// </summary>
        public async Task<List<RoleLoginAccessDto>> GetAllRoleLoginAccessAsync()
        {
            var settings = await _context.RoleLoginAccessControls
                .OrderBy(r => r.Role)
                .ToListAsync();

            return settings.Select(s => new RoleLoginAccessDto
            {
                Id = s.Id,
                Role = s.Role,
                IsLoginEnabled = s.IsLoginEnabled,
                DisabledMessage = s.DisabledMessage,
                ChangeReason = s.ChangeReason,
                IsScheduled = s.IsScheduled,
                ScheduledStartTime = s.ScheduledStartTime,
                ScheduledEndTime = s.ScheduledEndTime,
                ScheduledReason = s.ScheduledReason,
                LastModifiedBy = s.LastModifiedBy,
                LastModifiedAt = s.LastModifiedAt,
                CreatedAt = s.CreatedAt
            }).ToList();
        }

        /// <summary>
        /// Gets login access control settings for a specific role
        /// </summary>
        public async Task<RoleLoginAccessDto?> GetRoleLoginAccessAsync(string role)
        {
            var setting = await _context.RoleLoginAccessControls
                .FirstOrDefaultAsync(r => r.Role == role);

            if (setting == null)
                return null;

            return new RoleLoginAccessDto
            {
                Id = setting.Id,
                Role = setting.Role,
                IsLoginEnabled = setting.IsLoginEnabled,
                DisabledMessage = setting.DisabledMessage,
                ChangeReason = setting.ChangeReason,
                IsScheduled = setting.IsScheduled,
                ScheduledStartTime = setting.ScheduledStartTime,
                ScheduledEndTime = setting.ScheduledEndTime,
                ScheduledReason = setting.ScheduledReason,
                LastModifiedBy = setting.LastModifiedBy,
                LastModifiedAt = setting.LastModifiedAt,
                CreatedAt = setting.CreatedAt
            };
        }

        /// <summary>
        /// Creates or updates login access control settings for a role
        /// </summary>
        public async Task<RoleLoginAccessDto> UpdateRoleLoginAccessAsync(
            string role,
            UpdateRoleLoginAccessRequest request,
            Guid modifiedBy)
        {
            var existing = await _context.RoleLoginAccessControls
                .FirstOrDefaultAsync(r => r.Role == role);

            if (existing == null)
            {
                // Create new setting
                var newSetting = new RoleLoginAccessControl
                {
                    Id = Guid.NewGuid(),
                    Role = role,
                    IsLoginEnabled = request.IsLoginEnabled,
                    DisabledMessage = request.DisabledMessage,
                    ChangeReason = request.ChangeReason,
                    LastModifiedBy = modifiedBy,
                    LastModifiedAt = DateTime.UtcNow,
                    CreatedAt = DateTime.UtcNow
                };

                _context.RoleLoginAccessControls.Add(newSetting);
                await _context.SaveChangesAsync();

                return new RoleLoginAccessDto
                {
                    Id = newSetting.Id,
                    Role = newSetting.Role,
                    IsLoginEnabled = newSetting.IsLoginEnabled,
                    DisabledMessage = newSetting.DisabledMessage,
                    ChangeReason = newSetting.ChangeReason,
                    LastModifiedBy = newSetting.LastModifiedBy,
                    LastModifiedAt = newSetting.LastModifiedAt,
                    CreatedAt = newSetting.CreatedAt
                };
            }

            // Update existing setting
            existing.IsLoginEnabled = request.IsLoginEnabled;
            existing.DisabledMessage = request.DisabledMessage;
            existing.ChangeReason = request.ChangeReason;
            existing.LastModifiedBy = modifiedBy;
            existing.LastModifiedAt = DateTime.UtcNow;

            _context.RoleLoginAccessControls.Update(existing);
            await _context.SaveChangesAsync();

            return new RoleLoginAccessDto
            {
                Id = existing.Id,
                Role = existing.Role,
                IsLoginEnabled = existing.IsLoginEnabled,
                DisabledMessage = existing.DisabledMessage,
                ChangeReason = existing.ChangeReason,
                IsScheduled = existing.IsScheduled,
                ScheduledStartTime = existing.ScheduledStartTime,
                ScheduledEndTime = existing.ScheduledEndTime,
                ScheduledReason = existing.ScheduledReason,
                LastModifiedBy = existing.LastModifiedBy,
                LastModifiedAt = existing.LastModifiedAt,
                CreatedAt = existing.CreatedAt
            };
        }

        /// <summary>
        /// Schedules automatic disable/enable of login access for a role
        /// </summary>
        public async Task<RoleLoginAccessDto> ScheduleRoleAccessAsync(
            string role,
            ScheduleRoleAccessRequest request,
            Guid modifiedBy)
        {
            // Get the admin user to retrieve their timezone preference
            var adminUser = await _context.Users.FindAsync(modifiedBy);
            if (adminUser == null)
            {
                throw new ResourceNotFoundException("User", modifiedBy.ToString());
            }

            // Use provided timezone override, or fall back to user's stored timezone, default to Nigeria
            var timezoneOffsetHours = request.TimezoneOffsetHours.HasValue 
                ? request.TimezoneOffsetHours.Value 
                : (adminUser?.TimezoneOffsetHours ?? 1);

            // Validate timezone offset is reasonable (-14 to +14 hours)
            if (Math.Abs(timezoneOffsetHours) > 14)
            {
                throw new ValidationException("Timezone offset must be between -14 and +14 hours");
            }

            // Convert user's local times to UTC based on timezone offset
            // If user (Nigeria, UTC+1) submits 3:00 PM, subtract 1 hour to get 2:00 PM UTC
            var utcStartTime = request.ScheduledStartTime.AddHours(-timezoneOffsetHours);
            var utcEndTime = request.ScheduledEndTime.AddHours(-timezoneOffsetHours);

            // Validate schedule times
            if (utcStartTime >= utcEndTime)
            {
                throw new ValidationException("Scheduled start time must be before end time");
            }

            // Allow scheduling up to 5 seconds in the past to account for clockskew/network delay
            var utcNowWithBuffer = DateTime.UtcNow.AddSeconds(-5);
            if (utcStartTime < utcNowWithBuffer)
            {
                throw new ValidationException(
                    $"Scheduled start time cannot be in the past. Current UTC time is {DateTime.UtcNow:O}, " +
                    $"your local start time (timezone UTC{(timezoneOffsetHours > 0 ? "+" : "")}{timezoneOffsetHours}) converts to {utcStartTime:O} UTC");
            }

            var existing = await _context.RoleLoginAccessControls
                .FirstOrDefaultAsync(r => r.Role == role);

            if (existing == null)
            {
                // Create new setting with schedule
                var newSetting = new RoleLoginAccessControl
                {
                    Id = Guid.NewGuid(),
                    Role = role,
                    IsLoginEnabled = true,
                    DisabledMessage = request.DisabledMessage,
                    ChangeReason = null,
                    IsScheduled = true,
                    ScheduledStartTime = utcStartTime,  // Store in UTC
                    ScheduledEndTime = utcEndTime,      // Store in UTC
                    ScheduledReason = request.ScheduledReason,
                    LastModifiedBy = modifiedBy,
                    LastModifiedAt = DateTime.UtcNow,
                    CreatedAt = DateTime.UtcNow
                };

                _context.RoleLoginAccessControls.Add(newSetting);
                await _context.SaveChangesAsync();

                return new RoleLoginAccessDto
                {
                    Id = newSetting.Id,
                    Role = newSetting.Role,
                    IsLoginEnabled = newSetting.IsLoginEnabled,
                    DisabledMessage = newSetting.DisabledMessage,
                    ChangeReason = newSetting.ChangeReason,
                    IsScheduled = newSetting.IsScheduled,
                    ScheduledStartTime = newSetting.ScheduledStartTime,
                    ScheduledEndTime = newSetting.ScheduledEndTime,
                    ScheduledReason = newSetting.ScheduledReason,
                    LastModifiedBy = newSetting.LastModifiedBy,
                    LastModifiedAt = newSetting.LastModifiedAt,
                    CreatedAt = newSetting.CreatedAt
                };
            }

            // Update existing setting with schedule
            existing.IsScheduled = true;
            existing.ScheduledStartTime = utcStartTime;  // Store in UTC
            existing.ScheduledEndTime = utcEndTime;      // Store in UTC
            existing.ScheduledReason = request.ScheduledReason;
            existing.DisabledMessage = request.DisabledMessage ?? existing.DisabledMessage;
            existing.LastModifiedBy = modifiedBy;
            existing.LastModifiedAt = DateTime.UtcNow;

            _context.RoleLoginAccessControls.Update(existing);
            await _context.SaveChangesAsync();

            return new RoleLoginAccessDto
            {
                Id = existing.Id,
                Role = existing.Role,
                IsLoginEnabled = existing.IsLoginEnabled,
                DisabledMessage = existing.DisabledMessage,
                ChangeReason = existing.ChangeReason,
                IsScheduled = existing.IsScheduled,
                ScheduledStartTime = existing.ScheduledStartTime,
                ScheduledEndTime = existing.ScheduledEndTime,
                ScheduledReason = existing.ScheduledReason,
                LastModifiedBy = existing.LastModifiedBy,
                LastModifiedAt = existing.LastModifiedAt,
                CreatedAt = existing.CreatedAt
            };
        }

        /// <summary>
        /// Cancels a scheduled access disable/enable
        /// </summary>
        public async Task<RoleLoginAccessDto> CancelScheduledAccessAsync(string role, Guid modifiedBy)
        {
            var existing = await _context.RoleLoginAccessControls
                .FirstOrDefaultAsync(r => r.Role == role);

            if (existing == null)
            {
                throw new ResourceNotFoundException("RoleLoginAccessControl", role);
            }

            if (!existing.IsScheduled)
            {
                throw new ValidationException($"No active schedule found for role: {role}");
            }

            // Clear schedule
            existing.IsScheduled = false;
            existing.ScheduledStartTime = null;
            existing.ScheduledEndTime = null;
            existing.ScheduledReason = null;
            existing.LastModifiedBy = modifiedBy;
            existing.LastModifiedAt = DateTime.UtcNow;

            _context.RoleLoginAccessControls.Update(existing);
            await _context.SaveChangesAsync();

            return new RoleLoginAccessDto
            {
                Id = existing.Id,
                Role = existing.Role,
                IsLoginEnabled = existing.IsLoginEnabled,
                DisabledMessage = existing.DisabledMessage,
                ChangeReason = existing.ChangeReason,
                IsScheduled = existing.IsScheduled,
                ScheduledStartTime = existing.ScheduledStartTime,
                ScheduledEndTime = existing.ScheduledEndTime,
                ScheduledReason = existing.ScheduledReason,
                LastModifiedBy = existing.LastModifiedBy,
                LastModifiedAt = existing.LastModifiedAt,
                CreatedAt = existing.CreatedAt
            };
        }
    }
}
