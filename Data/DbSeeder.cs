using MMGT.API.Models;
using Microsoft.EntityFrameworkCore;

namespace MMGT.API.Data
{
    public static class DbSeeder
    {
        public static async Task SeedAsync(ApplicationDbContext context)
        {
            // Check if data already exists
            if (await context.Users.AnyAsync())
                return;

            // Create Super Admin
            var superAdmin = new User
            {
                Id = Guid.NewGuid(),
                Email = "superadmin@pfa.com",
                FirstName = "Super",
                LastName = "Admin",
                Role = "SUPER_ADMIN",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("super123"),
                IsFirstLogin = false,
                IsActive = true,
                CreatedAt = DateTime.Now,
                UpdatedAt = DateTime.Now
            };

            // Create Regular Admin
            var admin = new User
            {
                Id = Guid.NewGuid(),
                Email = "admin@pfa.com",
                FirstName = "PFA",
                LastName = "Reviewer",
                Role = "ADMIN",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("admin123"),
                IsFirstLogin = false,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            // Create PMI Company
            var pmiCompany = new PMICompany
            {
                Id = Guid.NewGuid(),
                PmiName = "Sample PMI Company",
                EmailAddress = "pmi@company.com",
                ResourcePersonName = "John Doe",
                ResourcePersonEmail = "john.doe@company.com",
                PhoneNumber = "+2348012345678",
                Status = "ACTIVE",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            // Create PMI Client User
            var pmiClient = new User
            {
                Id = Guid.NewGuid(),
                Email = "pmi@company.com",
                FirstName = "John",
                LastName = "Doe",
                Role = "CLIENT",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("temp123"),
                IsFirstLogin = true,
                IsActive = true,
                PMICompanyId = pmiCompany.Id,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            // Add to context
            context.Users.AddRange(superAdmin, admin, pmiClient);
            context.PMICompanies.Add(pmiCompany);

            // Save changes
            await context.SaveChangesAsync();
        }
    }
}