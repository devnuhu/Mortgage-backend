using MMGT.API.Services.Interfaces;
using MMGT.API.DTOs.PMI;
using MMGT.API.DTOs;
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
            // Get all PMI companies with application counts in a single query
            var pmiCompanies = await _context.PMICompanies
                .AsNoTracking()
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
                    TotalApplications = p.Applications.Count,
                    PendingApplications = p.Applications.Count(a => a.Status == "submitted" || a.Status == "under_review"),
                    ApprovedApplications = p.Applications.Count(a => a.Status == "approved")
                })
                .ToListAsync();

            return pmiCompanies;
        }

        public async Task<PaginatedResponse<PMICompanyDto>> GetAllPMICompaniesPagedAsync(PaginationRequest pagination)
        {
            pagination.Validate();

            var query = _context.PMICompanies
                .AsNoTracking()
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
                    TotalApplications = p.Applications.Count,
                    PendingApplications = p.Applications.Count(a => a.Status == "submitted" || a.Status == "under_review"),
                    ApprovedApplications = p.Applications.Count(a => a.Status == "approved")
                });

            var totalCount = await query.CountAsync();

            var data = await query
                .Skip((pagination.PageNumber - 1) * pagination.PageSize)
                .Take(pagination.PageSize)
                .ToListAsync();

            return new PaginatedResponse<PMICompanyDto>
            {
                Data = data,
                TotalCount = totalCount,
                PageNumber = pagination.PageNumber,
                PageSize = pagination.PageSize
            };
        }

        public async Task<PMICompanyDto> GetPMICompanyByIdAsync(Guid id)
        {
            var pmiCompany = await _context.PMICompanies
                .AsNoTracking()
                .Where(p => p.Id == id)
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
                    TotalApplications = p.Applications.Count,
                    PendingApplications = p.Applications.Count(a => a.Status == "submitted" || a.Status == "under_review"),
                    ApprovedApplications = p.Applications.Count(a => a.Status == "approved")
                })
                .FirstOrDefaultAsync();

            if (pmiCompany == null)
                throw new Exception("PMI Company not found");

            return pmiCompany;
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