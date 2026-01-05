using MMGT.API.Services.Interfaces;
using MMGT.API.DTOs.Application;
using MMGT.API.Data;
using MMGT.API.Models;
using Microsoft.EntityFrameworkCore;

namespace MMGT.API.Services
{
    public class ApplicationService : IApplicationService
    {
        private readonly ApplicationDbContext _context;
        private readonly IEmailService _emailService;

        public ApplicationService(ApplicationDbContext context, IEmailService emailService)
        {
            _context = context;
            _emailService = emailService;
        }

        private static string NormalizeStatus(string? status)
        {
            return (status ?? string.Empty).Trim().ToLowerInvariant();
        }

        public async Task<List<ApplicationDto>> GetAllApplicationsAsync()
        {
            var applications = await _context.Applications
                .Include(a => a.User)
                .Include(a => a.PmiCompany)
                .Include(a => a.Documents)
                .OrderByDescending(a => a.CreatedAt)
                .Select(a => new ApplicationDto
                {
                    Id = a.Id,
                    ApplicationNumber = a.ApplicationNumber,
                    Status = a.Status,
                    Priority = a.Priority,
                    FirstName = a.FirstName,
                    MiddleName = a.MiddleName,
                    LastName = a.LastName,
                    RsaPin = a.RsaPin,
                    EquityValue = a.EquityValue,
                    SupplementaryValue = a.SupplementaryValue,
                    LoanAmount = a.LoanAmount,
                    PropertyValue = a.PropertyValue,
                    PmiRepName = a.PmiRepName,
                    PmiRepEmail = a.PmiRepEmail,
                    PmiCompanyName = a.PmiCompanyName,
                    CreatedAt = a.CreatedAt,
                    UpdatedAt = a.UpdatedAt,
                    SubmittedAt = a.SubmittedAt,
                    UserId = a.UserId,
                    UserName = $"{a.User.FirstName} {a.User.LastName}",
                    PmiCompanyId = a.PmiCompanyId,
                    CompanyName = a.PmiCompany.PmiName,
                    DocumentCount = a.Documents.Count
                })
                .ToListAsync();

            return applications;
        }

        public async Task<List<ApplicationDto>> GetApplicationsByUserAsync(Guid userId)
        {
            var applications = await _context.Applications
                .Include(a => a.User)
                .Include(a => a.PmiCompany)
                .Include(a => a.Documents)
                .Where(a => a.UserId == userId)
                .OrderByDescending(a => a.CreatedAt)
                .Select(a => new ApplicationDto
                {
                    Id = a.Id,
                    ApplicationNumber = a.ApplicationNumber,
                    Status = a.Status,
                    Priority = a.Priority,
                    FirstName = a.FirstName,
                    MiddleName = a.MiddleName,
                    LastName = a.LastName,
                    RsaPin = a.RsaPin,
                    EquityValue = a.EquityValue,
                    SupplementaryValue = a.SupplementaryValue,
                    LoanAmount = a.LoanAmount,
                    PropertyValue = a.PropertyValue,
                    PmiRepName = a.PmiRepName,
                    PmiRepEmail = a.PmiRepEmail,
                    PmiCompanyName = a.PmiCompanyName,
                    CreatedAt = a.CreatedAt,
                    UpdatedAt = a.UpdatedAt,
                    SubmittedAt = a.SubmittedAt,
                    UserId = a.UserId,
                    UserName = $"{a.User.FirstName} {a.User.LastName}",
                    PmiCompanyId = a.PmiCompanyId,
                    CompanyName = a.PmiCompany.PmiName,
                    DocumentCount = a.Documents.Count
                })
                .ToListAsync();

            return applications;
        }

        public async Task<List<ApplicationDto>> GetApplicationsByPMIAsync(Guid pmiCompanyId)
        {
            var applications = await _context.Applications
                .Include(a => a.User)
                .Include(a => a.PmiCompany)
                .Include(a => a.Documents)
                .Where(a => a.PmiCompanyId == pmiCompanyId)
                .OrderByDescending(a => a.CreatedAt)
                .Select(a => new ApplicationDto
                {
                    Id = a.Id,
                    ApplicationNumber = a.ApplicationNumber,
                    Status = a.Status,
                    Priority = a.Priority,
                    FirstName = a.FirstName,
                    MiddleName = a.MiddleName,
                    LastName = a.LastName,
                    RsaPin = a.RsaPin,
                    EquityValue = a.EquityValue,
                    SupplementaryValue = a.SupplementaryValue,
                    LoanAmount = a.LoanAmount,
                    PropertyValue = a.PropertyValue,
                    PmiRepName = a.PmiRepName,
                    PmiRepEmail = a.PmiRepEmail,
                    PmiCompanyName = a.PmiCompanyName,
                    CreatedAt = a.CreatedAt,
                    UpdatedAt = a.UpdatedAt,
                    SubmittedAt = a.SubmittedAt,
                    UserId = a.UserId,
                    UserName = $"{a.User.FirstName} {a.User.LastName}",
                    PmiCompanyId = a.PmiCompanyId,
                    CompanyName = a.PmiCompany.PmiName,
                    DocumentCount = a.Documents.Count
                })
                .ToListAsync();

            return applications;
        }

        public async Task<ApplicationDto> GetApplicationByIdAsync(Guid id)
        {
            var application = await _context.Applications
                .Include(a => a.User)
                .Include(a => a.PmiCompany)
                .Include(a => a.Documents)
                .FirstOrDefaultAsync(a => a.Id == id);

            if (application == null)
                throw new Exception("Application not found");

            return new ApplicationDto
            {
                Id = application.Id,
                ApplicationNumber = application.ApplicationNumber,
                Status = application.Status,
                Priority = application.Priority,
                FirstName = application.FirstName,
                MiddleName = application.MiddleName,
                LastName = application.LastName,
                RsaPin = application.RsaPin,
                EquityValue = application.EquityValue,
                SupplementaryValue = application.SupplementaryValue,
                LoanAmount = application.LoanAmount,
                PropertyValue = application.PropertyValue,
                PmiRepName = application.PmiRepName,
                PmiRepEmail = application.PmiRepEmail,
                PmiCompanyName = application.PmiCompanyName,
                CreatedAt = application.CreatedAt,
                UpdatedAt = application.UpdatedAt,
                SubmittedAt = application.SubmittedAt,
                UserId = application.UserId,
                UserName = $"{application.User.FirstName} {application.User.LastName}",
                PmiCompanyId = application.PmiCompanyId,
                CompanyName = application.PmiCompany.PmiName,
                DocumentCount = application.Documents.Count
            };
        }

        public async Task<ApplicationDto> CreateApplicationAsync(Guid userId, CreateApplicationRequest request)
        {
            var user = await _context.Users
                .Include(u => u.PMICompany)
                .FirstOrDefaultAsync(u => u.Id == userId);

            if (user == null)
                throw new Exception("User not found");

            if (user.PMICompany == null)
                throw new Exception("User is not associated with a PMI company");

            var application = new Application
            {
                Id = Guid.NewGuid(),
                ApplicationNumber = GenerateApplicationNumber(),
                Status = "draft",
                Priority = "medium",
                FirstName = request.FirstName,
                MiddleName = request.MiddleName,
                LastName = request.LastName,
                RsaPin = request.RsaPin,
                EquityValue = request.EquityValue,
                SupplementaryValue = request.SupplementaryValue,
                LoanAmount = request.LoanAmount,
                PropertyValue = request.PropertyValue,
                PmiRepName = $"{user.FirstName} {user.LastName}",
                PmiRepEmail = user.Email,
                PmiCompanyName = user.PMICompany.PmiName,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                UserId = userId,
                PmiCompanyId = user.PMICompany.Id
            };

            _context.Applications.Add(application);
            await _context.SaveChangesAsync();

            // notify mortgage.pmi.com about new application
            try
            {
                var toEmail = _context.PMICompanies
                    .Where(p => p.PmiName.ToLower() == "mortgage.pmi.com")
                    .Select(p => p.EmailAddress)
                    .FirstOrDefault();

                if (!string.IsNullOrWhiteSpace(toEmail))
                {
                    await _emailService.SendApplicationCreatedNotificationAsync(toEmail, application.Id, application.ApplicationNumber, $"{application.FirstName} {application.LastName}");
                }
            }
            catch (Exception ex)
            {
                // keep existing behavior; log or ignore failure so creation still succeeds
                // If a logging service exists prefer to log. For now swallow to avoid breaking behavior.
            }

            return await GetApplicationByIdAsync(application.Id);
        }

        public async Task<ApplicationDto> UpdateApplicationStatusAsync(Guid id, Guid reviewerId, UpdateApplicationStatusRequest request)
        {
            var application = await _context.Applications
                .Include(a => a.User)
                .Include(a => a.PmiCompany)
                .FirstOrDefaultAsync(a => a.Id == id);

            if (application == null)
                throw new Exception("Application not found");

            var oldStatus = application.Status;
            application.Status = request.Status;
            application.UpdatedAt = DateTime.UtcNow;

            // Add to review history
            var reviewHistory = new ApplicationReviewHistory
            {
                Id = Guid.NewGuid(),
                ApplicationId = id,
                ReviewedById = reviewerId,
                Status = request.Status,
                Comments = request.Comments,
                CreatedAt = DateTime.UtcNow
            };

            _context.ApplicationReviewHistory.Add(reviewHistory);
            await _context.SaveChangesAsync();

            // Send notifications: to application owner (app.PmiRepEmail) and mortgage.pmi.com
            try
            {
                var applicantEmail = application.PmiRepEmail; // PMI rep (owner)
                var applicantName = $"{application.FirstName} {application.LastName}";

                if (!string.IsNullOrWhiteSpace(applicantEmail))
                {
                    await _emailService.SendApplicationStatusUpdatedAsync(applicantEmail, application.Id, application.ApplicationNumber, applicantName, request.Status, request.Comments);
                }

                var pmiEmail = _context.PMICompanies
                    .Where(p => p.PmiName.ToLower() == "mortgage.pmi.com")
                    .Select(p => p.EmailAddress)
                    .FirstOrDefault();

                if (!string.IsNullOrWhiteSpace(pmiEmail))
                {
                    await _emailService.SendApplicationStatusUpdatedAsync(pmiEmail, application.Id, application.ApplicationNumber, applicantName, request.Status, request.Comments);
                }
            }
            catch
            {
                // swallow email exceptions to avoid breaking status update
            }

            return await GetApplicationByIdAsync(application.Id);
        }

        public async Task<ApplicationDto> UpdateApplicationAsync(Guid id, Guid userId, UpdateApplicationRequest request)
        {
            var app = await _context.Applications.FindAsync(id);
            if (app == null) throw new Exception("Application not found");

            if (app.UserId != userId) throw new Exception("Unauthorized to edit this application");

            // Only allow edits when application is in draft, rejected or resubmitted state
            var appStatus = NormalizeStatus(app.Status);
            if (appStatus != "draft" && appStatus != "rejected" && appStatus != "resubmitted")
                throw new Exception("Only draft, rejected or resubmitted applications can be edited");

            // Apply allowed updates
            app.FirstName = request.FirstName ?? app.FirstName;
            app.MiddleName = request.MiddleName ?? app.MiddleName;
            app.LastName = request.LastName ?? app.LastName;
            app.RsaPin = request.RsaPin ?? app.RsaPin;
            app.EquityValue = request.EquityValue ?? app.EquityValue;
            app.SupplementaryValue = request.SupplementaryValue ?? app.SupplementaryValue;
            app.LoanAmount = request.LoanAmount ?? app.LoanAmount;
            app.PropertyValue = request.PropertyValue ?? app.PropertyValue;
            app.Priority = request.Priority ?? app.Priority;
            app.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            return await GetApplicationByIdAsync(app.Id);
        }

        public async Task<bool> SubmitApplicationAsync(Guid id)
        {
            var application = await _context.Applications.FindAsync(id);
            if (application == null)
                return false;

            // Allow submission from draft, rejected or resubmitted states
            var appStatus = NormalizeStatus(application.Status);
            if (appStatus != "draft" && appStatus != "rejected" && appStatus != "resubmitted")
                throw new Exception("Only draft, rejected or resubmitted applications can be submitted");

            var previousStatus = appStatus;

            // If application was rejected or marked resubmitted and client resubmits after making updates,
            // set status to "returned" so admin can see it as a returned application
            if (previousStatus == "rejected" || previousStatus == "resubmitted")
            {
                application.Status = "returned";

                // record review history entry indicating the client returned the application
                var reviewHistory = new ApplicationReviewHistory
                {
                    Id = Guid.NewGuid(),
                    ApplicationId = id,
                    ReviewedById = application.UserId, // performed by client
                    Status = "returned",
                    Comments = "Client returned application after making requested updates",
                    CreatedAt = DateTime.UtcNow
                };

                _context.ApplicationReviewHistory.Add(reviewHistory);
            }
            else
            {
                application.Status = "submitted";
            }

            application.SubmittedAt = DateTime.UtcNow;
            application.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            // Notify mortgage.pmi.com about submission as well
            try
            {
                var pmiEmail = _context.PMICompanies
                    .Where(p => p.PmiName.ToLower() == "mortgage.pmi.com")
                    .Select(p => p.EmailAddress)
                    .FirstOrDefault();

                var applicantName = $"{application.FirstName} {application.LastName}";

                if (!string.IsNullOrWhiteSpace(pmiEmail))
                {
                    await _emailService.SendApplicationCreatedNotificationAsync(pmiEmail, application.Id, application.ApplicationNumber, applicantName);
                }
            }
            catch
            {
                // swallow email errors
            }

            return true;
        }

        public async Task<bool> DeleteApplicationAsync(Guid id)
        {
            var application = await _context.Applications.FindAsync(id);
            if (application == null)
                return false;

            if (NormalizeStatus(application.Status) != "draft")
                throw new Exception("Only draft applications can be deleted");

            _context.Applications.Remove(application);
            await _context.SaveChangesAsync();
            return true;
        }

        private string GenerateApplicationNumber()
        {
            return $"APP-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString().Substring(0, 8).ToUpper()}";
        }

        public async Task<List<ReviewHistoryDto>> GetApplicationReviewHistoryAsync(Guid applicationId)
        {
            var reviewHistory = await _context.ApplicationReviewHistory
                .Include(rh => rh.ReviewedBy)
                .Where(rh => rh.ApplicationId == applicationId)
                .OrderByDescending(rh => rh.CreatedAt)
                .ToListAsync();

            return reviewHistory.Select(rh => new ReviewHistoryDto
            {
                Id = rh.Id,
                Status = rh.Status,
                Comments = rh.Comments,
                CreatedAt = rh.CreatedAt,
                ReviewedById = rh.ReviewedById,
                ReviewedByName = $"{rh.ReviewedBy.FirstName} {rh.ReviewedBy.LastName}",
                ReviewedByEmail = rh.ReviewedBy.Email
            }).ToList();
        }
    }
}