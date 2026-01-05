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

        public UserService(ApplicationDbContext context, IEmailService emailService)
        {
            _context = context;
            _emailService = emailService;
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

            // Determine password and token behavior:
            // - If admin provided a TemporaryPassword in the request, use it (user can login immediately).
            // - Otherwise, generate a placeholder password and generate a first-login token (emailed to the user).
            string passwordToHash;
            string? hashedToken = null;
            DateTime? expiry = null;
            string? tokenToSend = null;

            if (!string.IsNullOrWhiteSpace(request.TemporaryPassword))
            {
                passwordToHash = request.TemporaryPassword;
            }
            else
            {
                // Generate a safe placeholder password so account has a valid hash
                passwordToHash = GenerateTempPassword();

                // Generate one-time token and store hashed token + expiry for first-login flow
                tokenToSend = SecurityHelpers.GenerateUrlSafeToken();
                hashedToken = SecurityHelpers.HashToken(tokenToSend);
                expiry = DateTime.UtcNow.AddHours(2);
            }

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

            // Only send first-login link email when a token was generated (i.e., admin didn't provide a temp password)
            if (tokenToSend != null)
            {
                try
                {
                    _ = Task.Run(() => _emailService.SendFirstLoginLinkAsync(user.Id, user.Email, tokenToSend));
                }
                catch (Exception ex)
                {
                    Console.WriteLine("Failed to queue/send first-login email: " + ex.Message);
                }
            }

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

        public async Task<string?> ResetUserPasswordAsync(Guid userId)
        {
            var user = await _context.Users.FindAsync(userId);
            if (user == null || !user.IsActive)
                return null;

            // Generate new temporary password
            var tempPassword = GenerateTempPassword();
            var hashedPassword = BCrypt.Net.BCrypt.HashPassword(tempPassword);

            // Update user password
            user.PasswordHash = hashedPassword;
            user.IsFirstLogin = true;
            user.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            // TODO: Send email with new temporary password to user.Email
            return tempPassword;
        }

        public async Task<bool> ResendFirstLoginLinkAsync(Guid userId)
        {
            var user = await _context.Users.FindAsync(userId);
            if (user == null || !user.IsActive) return false;

            var token = SecurityHelpers.GenerateUrlSafeToken();
            user.FirstLoginTokenHash = SecurityHelpers.HashToken(token);
            user.FirstLoginTokenExpiresAt = DateTime.UtcNow.AddHours(2);
            user.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            try
            {
                _ = Task.Run(() => _emailService.SendFirstLoginLinkAsync(user.Id, user.Email, token));
            }
            catch (Exception ex)
            {
                Console.WriteLine("Failed to resend first-login email: " + ex.Message);
            }

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
