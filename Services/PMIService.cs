using MMGT.API.Services.Interfaces;
using MMGT.API.DTOs.PMI;
using MMGT.API.Data;
using MMGT.API.Models;
using Microsoft.EntityFrameworkCore;

namespace MMGT.API.Services
{
    public class PMIService : IPMIService
    {
        private readonly ApplicationDbContext _context;

        public PMIService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<List<PMICompanyDto>> GetAllPMICompaniesAsync()
        {
            var pmiCompanies = await _context.PMICompanies
                .Select(p => new PMICompanyDto
                {
                    Id = p.Id,
                    PmiName = p.PmiName,
                    EmailAddress = p.EmailAddress,
                    ResourcePersonName = p.ResourcePersonName,
                    ResourcePersonEmail = p.ResourcePersonEmail,
                    PhoneNumber = p.PhoneNumber,
                    Status = p.Status,
                    CreatedAt = p.CreatedAt,
                    UpdatedAt = p.UpdatedAt,
                    TotalApplications = 0, // We'll calculate this separately if needed
                    PendingApplications = 0,
                    ApprovedApplications = 0
                })
                .ToListAsync();

            // Calculate application counts for each PMI company
            foreach (var pmi in pmiCompanies)
            {
                var applications = await _context.Applications
                    .Where(a => a.PmiCompanyId == pmi.Id)
                    .ToListAsync();

                pmi.TotalApplications = applications.Count;
                pmi.PendingApplications = applications.Count(a => a.Status == "submitted" || a.Status == "under_review");
                pmi.ApprovedApplications = applications.Count(a => a.Status == "approved");
            }

            return pmiCompanies;
        }

        public async Task<PMICompanyDto> GetPMICompanyByIdAsync(Guid id)
        {
            var pmiCompany = await _context.PMICompanies
                .FirstOrDefaultAsync(p => p.Id == id);

            if (pmiCompany == null)
                throw new Exception("PMI Company not found");

            // Calculate application counts
            var applications = await _context.Applications
                .Where(a => a.PmiCompanyId == id)
                .ToListAsync();

            return new PMICompanyDto
            {
                Id = pmiCompany.Id,
                PmiName = pmiCompany.PmiName,
                EmailAddress = pmiCompany.EmailAddress,
                ResourcePersonName = pmiCompany.ResourcePersonName,
                ResourcePersonEmail = pmiCompany.ResourcePersonEmail,
                PhoneNumber = pmiCompany.PhoneNumber,
                Status = pmiCompany.Status,
                CreatedAt = pmiCompany.CreatedAt,
                UpdatedAt = pmiCompany.UpdatedAt,
                TotalApplications = applications.Count,
                PendingApplications = applications.Count(a => a.Status == "submitted" || a.Status == "under_review"),
                ApprovedApplications = applications.Count(a => a.Status == "approved")
            };
        }

        public async Task<PMICompanyDto> CreatePMICompanyAsync(CreatePMIRequest request)
        {
            // Check if email already exists
            if (await _context.PMICompanies.AnyAsync(p => p.EmailAddress == request.EmailAddress))
            {
                throw new Exception("Email address already exists");
            }

            var pmiCompany = new PMICompany
            {
                Id = Guid.NewGuid(),
                PmiName = request.PmiName,
                EmailAddress = request.EmailAddress,
                ResourcePersonName = request.ResourcePersonName,
                ResourcePersonEmail = request.ResourcePersonEmail,
                PhoneNumber = request.PhoneNumber,
                Status = "ACTIVE",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            _context.PMICompanies.Add(pmiCompany);
            await _context.SaveChangesAsync();

            return await GetPMICompanyByIdAsync(pmiCompany.Id);
        }

        public async Task<PMICompanyDto> UpdatePMICompanyAsync(Guid id, UpdatePMIRequest request)
        {
            var pmiCompany = await _context.PMICompanies.FindAsync(id);
            if (pmiCompany == null)
                throw new Exception("PMI Company not found");

            // Update only provided fields
            if (!string.IsNullOrEmpty(request.PmiName))
                pmiCompany.PmiName = request.PmiName;

            if (!string.IsNullOrEmpty(request.EmailAddress))
            {
                // Check if new email already exists
                if (await _context.PMICompanies.AnyAsync(p => p.EmailAddress == request.EmailAddress && p.Id != id))
                {
                    throw new Exception("Email address already exists");
                }
                pmiCompany.EmailAddress = request.EmailAddress;
            }

            if (!string.IsNullOrEmpty(request.ResourcePersonName))
                pmiCompany.ResourcePersonName = request.ResourcePersonName;

            if (!string.IsNullOrEmpty(request.ResourcePersonEmail))
                pmiCompany.ResourcePersonEmail = request.ResourcePersonEmail;

            if (!string.IsNullOrEmpty(request.PhoneNumber))
                pmiCompany.PhoneNumber = request.PhoneNumber;

            if (!string.IsNullOrEmpty(request.Status))
                pmiCompany.Status = request.Status;

            pmiCompany.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            return await GetPMICompanyByIdAsync(pmiCompany.Id);
        }

        public async Task<bool> DeletePMICompanyAsync(Guid id)
        {
            var pmiCompany = await _context.PMICompanies.FindAsync(id);
            if (pmiCompany == null)
                return false;

            // Check if there are any active applications
            var hasActiveApplications = await _context.Applications
                .AnyAsync(a => a.PmiCompanyId == id && (a.Status == "submitted" || a.Status == "under_review"));

            if (hasActiveApplications)
            {
                throw new Exception("Cannot delete PMI company with active applications");
            }

            pmiCompany.Status = "INACTIVE";
            pmiCompany.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            return true;
        }

        public async Task<bool> UpdatePMIStatusAsync(Guid id, string status)
        {
            var pmiCompany = await _context.PMICompanies.FindAsync(id);
            if (pmiCompany == null)
                return false;

            pmiCompany.Status = status;
            pmiCompany.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            return true;
        }
    }
}