using MMGT.API.Services.Interfaces;
using MMGT.API.DTOs.User;
using MMGT.API.Data;
using MMGT.API.Models;
using Microsoft.EntityFrameworkCore;
using BCrypt.Net;
using MMGT.API.Utils;
using System.Security.Cryptography;

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

            // A temporary password is always generated here and emailed to the user.
            // The user must change it at first sign-in (IsFirstLogin stays true).
            var temporaryPassword = GenerateTempPassword();

            var user = new User
            {
                Id = Guid.NewGuid(),
                Email = request.Email,
                FirstName = request.FirstName,
                LastName = request.LastName,
                Role = request.Role,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(temporaryPassword),
                IsFirstLogin = true,
                IsActive = true,
                PMICompanyId = request.PMICompanyId,
                PMIEmail = pmiCompany?.EmailAddress,
                FirstLoginTokenHash = null,
                FirstLoginTokenExpiresAt = null,
                CreatedAt = now,
                UpdatedAt = now
            };

            _context.Users.Add(user);
            await _context.SaveChangesAsync();

            QueueTemporaryPasswordEmail(user.Email, user.FirstName, temporaryPassword, true);

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
            user.FirstLoginTokenHash = null;
            user.FirstLoginTokenExpiresAt = null;
            user.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            QueueTemporaryPasswordEmail(user.Email, user.FirstName, tempPassword, false);
            return tempPassword;
        }

        /// <summary>
        /// Kept under its old name because the controller calls it. New accounts no longer use a setup link,
        /// so this now generates a fresh temporary password and emails it again.
        /// </summary>
        public async Task<bool> ResendFirstLoginLinkAsync(Guid userId)
        {
            var user = await _context.Users.FindAsync(userId);
            if (user == null || !user.IsActive) return false;

            var tempPassword = GenerateTempPassword();
            user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(tempPassword);
            user.IsFirstLogin = true;
            user.FirstLoginTokenHash = null;
            user.FirstLoginTokenExpiresAt = null;
            user.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            QueueTemporaryPasswordEmail(user.Email, user.FirstName, tempPassword, true);
            return true;
        }

        /// <summary>Sends the email in the background. A failure is never allowed to break the request, and the password is never logged.</summary>
        private void QueueTemporaryPasswordEmail(string email, string firstName, string temporaryPassword, bool newAccount)
        {
            var emailService = _emailService;
            _ = Task.Run(async () =>
            {
                try
                {
                    await emailService.SendTemporaryPasswordAsync(email, firstName, temporaryPassword, newAccount);
                }
                catch (Exception ex)
                {
                    Console.WriteLine("Failed to send temporary password email: " + ex.GetType().Name);
                }
            });
        }

        /// <summary>12 characters from a cryptographic random source, with at least one upper case, lower case, digit and symbol.</summary>
        private string GenerateTempPassword()
        {
            const string upperCase = "ABCDEFGHJKMNPQRSTUVWXYZ";
            const string lowerCase = "abcdefghijkmnpqrstuvwxyz";
            const string numbers = "23456789";
            const string specialChars = "!@#$%&?";
            const string allChars = upperCase + lowerCase + numbers + specialChars;

            var password = new char[12];
            password[0] = upperCase[RandomNumberGenerator.GetInt32(upperCase.Length)];
            password[1] = lowerCase[RandomNumberGenerator.GetInt32(lowerCase.Length)];
            password[2] = numbers[RandomNumberGenerator.GetInt32(numbers.Length)];
            password[3] = specialChars[RandomNumberGenerator.GetInt32(specialChars.Length)];
            for (int i = 4; i < 12; i++)
            {
                password[i] = allChars[RandomNumberGenerator.GetInt32(allChars.Length)];
            }

            // Shuffle
            for (int i = password.Length - 1; i > 0; i--)
            {
                int j = RandomNumberGenerator.GetInt32(i + 1);
                (password[i], password[j]) = (password[j], password[i]);
            }

            return new string(password);
        }
    }
}