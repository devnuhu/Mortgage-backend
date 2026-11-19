using MMGT.API.Services.Interfaces;
using MMGT.API.DTOs.User;
using MMGT.API.Data;
using MMGT.API.Models;
using Microsoft.EntityFrameworkCore;
using BCrypt.Net;
using MMGT.API.Utils;

namespace MMGT.API.Services
{
    public class UserService : IUserService
    {
        private readonly ApplicationDbContext _context;
        private readonly IEmailService _emailService;
        private readonly IAccountEmailService _accountEmail;

        public UserService(ApplicationDbContext context, IEmailService emailService, IAccountEmailService accountEmail)
        {
            _context = context;
            _emailService = emailService;
            _accountEmail = accountEmail;
        }

        public async Task<UserDto> GetUserByIdAsync(Guid id)
        {
            var user = await _context.Users
                .Include(u => u.PMICompany)
                .FirstOrDefaultAsync(u => u.Id == id && u.IsActive);

            if (user == null)
                throw new Exception("User not found");

            return new UserDto
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
                PMICompanyId = user.PMICompanyId,
                PMICompanyName = user.PMICompany != null ? user.PMICompany.PmiName : null,
                PMIEmail = user.PMIEmail
            };
        }

        public async Task<UserDto> CreateUserAsync(CreateUserRequest request)
        {
            if (!UserRoles.IsValid(request.Role))
                throw new Exception($"'{request.Role}' is not a valid role. Valid roles: {string.Join(", ", UserRoles.Values)}.");
            // Check if email already exists
            if (await _context.Users.AnyAsync(u => u.Email == request.Email))
            {
                throw new Exception("Email already exists");
            }

            // Get PMI Company info if provided
            PMICompany? pmiCompany = null;
            if (request.PMICompanyId.HasValue)
            {
                pmiCompany = await _context.PMICompanies
                    .FirstOrDefaultAsync(p => p.Id == request.PMICompanyId.Value);
                if (pmiCompany == null)
                {
                    throw new Exception("PMI Company not found");
                }
            }

            var now = DateTime.UtcNow;

            // A strong temporary password is always generated and emailed to the new user. They must change it at first sign-in.
            var passwordToHash = TemporaryPasswordGenerator.Generate();
            string? hashedToken = null;
            DateTime? expiry = null;
            var hashedPassword = BCrypt.Net.BCrypt.HashPassword(passwordToHash);

            var user = new User
            {
                Id = Guid.NewGuid(),
                Email = request.Email,
                FirstName = request.FirstName,
                LastName = request.LastName,
                Role = request.Role,
                PasswordHash = hashedPassword,
                IsFirstLogin = true,
                IsActive = true,
                PMICompanyId = request.PMICompanyId,
                PMIEmail = pmiCompany?.EmailAddress,
                FirstLoginTokenHash = hashedToken,
                FirstLoginTokenExpiresAt = expiry,
                CreatedAt = now,
                UpdatedAt = now
            };

            _context.Users.Add(user);
            await _context.SaveChangesAsync();

            // Email the temporary password. If emails are on and the send fails, say so (the user exists; Reset password issues a new one).
            var welcomeSent = await _accountEmail.SendTemporaryPasswordAsync(user.Email, user.FirstName, passwordToHash, AccountEmailReason.NewAccount);
            if (!welcomeSent && _accountEmail.CanSend())
                throw new Exception("The user was created, but the welcome email could not be sent. Use Reset password to issue a temporary password.");

            return await GetUserByIdAsync(user.Id);
        }

        public async Task<bool> UpdateUserAsync(Guid id, object updateData)
        {
            var user = await _context.Users.FindAsync(id);
            if (user == null)
                return false;

            // TODO: Implement dynamic property update
            user.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            return true;
        }

        public async Task<bool> DeleteUserAsync(Guid id)
        {
            var user = await _context.Users.FindAsync(id);
            if (user == null)
                return false;

            user.IsActive = false;
            user.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            return true;
        }

        public async Task<bool> SetUserActiveAsync(Guid actorId, Guid userId, bool isActive, string? reason)
        {
            var user = await _context.Users.FindAsync(userId);
            if (user == null)
                return false;
        
            if (!isActive)
            {
                if (userId == actorId)
                    throw new Exception("You cannot deactivate your own account.");
        
                if (user.Role == "SUPER_ADMIN" && user.IsActive &&
                    !await _context.Users.AnyAsync(u => u.Id != userId && u.Role == "SUPER_ADMIN" && u.IsActive))
                    throw new Exception("The last active Super Admin cannot be deactivated.");
            }
        
            if (user.IsActive == isActive)
                return true;
        
            user.IsActive = isActive;
            user.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
        
            Console.WriteLine($"[USER-STATUS] user {userId} {(isActive ? "reactivated" : "deactivated")} by {actorId}. Reason: {(string.IsNullOrWhiteSpace(reason) ? "none given" : reason.Trim())}");
            return true;
        }
        
        public async Task<string?> ResetUserPasswordAsync(Guid userId)
        {
            var user = await _context.Users.FindAsync(userId);
            if (user == null || !user.IsActive)
                return null;

            // Generate new temporary password
            var tempPassword = TemporaryPasswordGenerator.Generate();
            var hashedPassword = BCrypt.Net.BCrypt.HashPassword(tempPassword);

            // Update user password
            user.PasswordHash = hashedPassword;
            user.IsFirstLogin = true;
            user.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            await _accountEmail.SendTemporaryPasswordAsync(user.Email, user.FirstName, tempPassword, AccountEmailReason.AdminReset);
            return tempPassword;
        }

        public async Task<bool> ResendFirstLoginLinkAsync(Guid userId)
        {
            var user = await _context.Users.FindAsync(userId);
            if (user == null || !user.IsActive) return false;

            var temporaryPassword = TemporaryPasswordGenerator.Generate();
            user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(temporaryPassword);
            user.IsFirstLogin = true;
            user.FirstLoginTokenHash = null;
            user.FirstLoginTokenExpiresAt = null;
            user.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            await _accountEmail.SendTemporaryPasswordAsync(user.Email, user.FirstName, temporaryPassword, AccountEmailReason.NewAccount);

            return true;
        }

        private string GenerateTempPassword()
        {
            const string upperCase = "ABCDEFGHJKMNPQRSTUVWXYZ";
            const string lowerCase = "abcdefghijkmnpqrstuvwxyz";
            const string numbers = "23456789";
            const string specialChars = "!@#$%^&*";

            var random = new Random();
            var password = new char[12];

            // Ensure at least one character from each category
            password[0] = upperCase[random.Next(upperCase.Length)];
            password[1] = lowerCase[random.Next(lowerCase.Length)];
            password[2] = numbers[random.Next(numbers.Length)];
            password[3] = specialChars[random.Next(specialChars.Length)];

            // Fill the rest randomly
            const string allChars = upperCase + lowerCase + numbers + specialChars;
            for (int i = 4; i < 12; i++)
            {
                password[i] = allChars[random.Next(allChars.Length)];
            }

            // Shuffle the array
            for (int i = password.Length - 1; i > 0; i--)
            {
                int j = random.Next(i + 1);
                (password[i], password[j]) = (password[j], password[i]);
            }

            return new string(password);
        }
    }
}
