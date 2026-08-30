using MMGT.API.Services.Interfaces;
using MMGT.API.DTOs.Application;
using MMGT.API.DTOs;
using MMGT.API.Data;
using MMGT.API.Models;
using MMGT.API.Exceptions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace MMGT.API.Services
{
    public class ApplicationService : IApplicationService
    {
        private readonly ApplicationDbContext _context;
        private readonly IEmailService _emailService;
        private readonly IReviewerAssignmentService _assignmentService;
        private readonly ISmsService _smsService;
        private readonly ILogger<ApplicationService> _logger;

        public ApplicationService(
            ApplicationDbContext context,
            IEmailService emailService,
            IReviewerAssignmentService assignmentService,
            ISmsService smsService,
            ILogger<ApplicationService> logger)
        {
            _context = context;
            _emailService = emailService;
            _assignmentService = assignmentService;
            _smsService = smsService;
            _logger = logger;
        }

        private static string NormalizeStatus(string? status)
        {
            return (status ?? string.Empty).Trim().ToLowerInvariant();
        }

        private IQueryable<ApplicationDto> GetApplicationsQuery()
        {
            return _context.Applications
                .AsNoTracking()
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
                    PhoneNumber = a.PhoneNumber,
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
                });
        }

        private IQueryable<ApplicationDto> GetApplicationsQueryExcludingDraft()
        {
            return GetApplicationsQuery()
                .Where(a => a.Status != "draft");
        }

        public async Task<List<ApplicationDto>> GetAllApplicationsAsync()
        {
            return await GetApplicationsQueryExcludingDraft().ToListAsync();
        }

        public async Task<PaginatedResponse<ApplicationDto>> GetAllApplicationsPagedAsync(PaginationRequest pagination)
        {
            pagination.Validate();

            var query = GetApplicationsQueryExcludingDraft();

            // Execute count first, then data - sequentially to avoid DbContext threading issues
            var totalCount = await query.CountAsync();
            var data = await query
                .Skip((pagination.PageNumber - 1) * pagination.PageSize)
                .Take(pagination.PageSize)
                .ToListAsync();

            return new PaginatedResponse<ApplicationDto>
            {
                Data = data,
                TotalCount = totalCount,
                PageNumber = pagination.PageNumber,
                PageSize = pagination.PageSize
            };
        }

        public async Task<List<ApplicationDto>> GetApplicationsByUserAsync(Guid userId)
        {
            return await GetApplicationsQuery()
                .Where(a => a.UserId == userId)
                .ToListAsync();
        }

        public async Task<PaginatedResponse<ApplicationDto>> GetApplicationsByUserPagedAsync(Guid userId, PaginationRequest pagination, string? searchTerm = null)
        {
            pagination.Validate();

            // Build the base query on Application entity (before Select)
            var applicationQuery = _context.Applications
                .AsNoTracking()
                .Where(a => a.UserId == userId);

            // Apply search filter BEFORE Select to avoid EF Core translation issues
            if (!string.IsNullOrWhiteSpace(searchTerm))
            {
                var normalizedSearch = searchTerm.Trim().ToLowerInvariant();
                applicationQuery = applicationQuery.Where(a =>
                    a.ApplicationNumber.ToLower().Contains(normalizedSearch) ||
                    a.FirstName.ToLower().Contains(normalizedSearch) ||
                    a.LastName.ToLower().Contains(normalizedSearch) ||
                    a.RsaPin.ToLower().Contains(normalizedSearch) ||
                    (a.PhoneNumber != null && a.PhoneNumber.ToLower().Contains(normalizedSearch)));
            }

            // Execute count first
            var totalCount = await applicationQuery.CountAsync();

            // Now apply the Select and pagination
            var data = await applicationQuery
                .OrderByDescending(a => a.CreatedAt)
                .Skip((pagination.PageNumber - 1) * pagination.PageSize)
                .Take(pagination.PageSize)
                .Select(a => new ApplicationDto
                {
                    Id = a.Id,
                    ApplicationNumber = a.ApplicationNumber,
                    Status = a.Status,
                    Priority = a.Priority,
                    FirstName = a.FirstName,
                    MiddleName = a.MiddleName,
                    LastName = a.LastName,
                    PhoneNumber = a.PhoneNumber,
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

            return new PaginatedResponse<ApplicationDto>
            {
                Data = data,
                TotalCount = totalCount,
                PageNumber = pagination.PageNumber,
                PageSize = pagination.PageSize
            };
        }

        public async Task<PaginatedResponse<ApplicationDetailsDto>> GetApplicationsByUserRejectedPagedAsync(Guid userId, PaginationRequest pagination)
        {
            pagination.Validate();

            // Get applications that match user and rejected status
            var applicationsData = await _context.Applications
                .AsNoTracking()
                .Where(a => a.UserId == userId && a.Status == "rejected")
                .OrderByDescending(a => a.UpdatedAt)
                .Select(a => new
                {
                    // Application properties
                    a.Id,
                    a.ApplicationNumber,
                    a.Status,
                    a.Priority,
                    a.FirstName,
                    a.MiddleName,
                    a.LastName,
                    a.PhoneNumber,
                    a.RsaPin,
                    a.EquityValue,
                    a.SupplementaryValue,
                    a.LoanAmount,
                    a.PropertyValue,
                    a.PmiCompanyId,
                    a.PmiCompanyName,
                    a.PmiRepName,
                    a.PmiRepEmail,
                    a.UserId,
                    a.CreatedAt,
                    a.SubmittedAt,
                    a.UpdatedAt,
                    // User info
                    UserFirstName = a.User!.FirstName,
                    UserLastName = a.User!.LastName,
                    UserEmail = a.User!.Email,
                    // PMI info
                    PmiName = a.PmiCompany!.PmiName,
                    DocumentCount = a.Documents.Count,
                    // Documents projection - EXCLUDES FileContent BLOB
                    Documents = a.Documents
                        .OrderByDescending(d => d.CreatedAt)
                        .Select(d => new ApplicationDocumentDetailDto
                        {
                            Id = d.Id,
                            DocumentType = d.DocumentType!.Name ?? "Unknown",
                            FileName = d.FileName,
                            ReviewStatus = d.ReviewStatus,
                            ReviewerComments = d.ReviewComments,
                            ReviewerName = d.Reviewer != null
                                ? $"{d.Reviewer.FirstName} {d.Reviewer.LastName}"
                                : null,
                            ReviewedAt = d.ReviewedAt,
                            UploadedAt = d.CreatedAt
                        })
                        .ToList(),
                    // Review history projection
                    ReviewHistory = a.ReviewHistory
                        .OrderByDescending(rh => rh.CreatedAt)
                        .Select(rh => new ReviewHistoryDetailDto
                        {
                            Id = rh.Id,
                            Status = rh.Status,
                            UpdatedStatus = rh.Status,
                            ReviewComments = rh.Comments,
                            ReviewerName = $"{rh.ReviewedBy.FirstName} {rh.ReviewedBy.LastName}".Trim(),
                            ReviewerEmail = rh.ReviewedBy.Email,
                            ReviewedAt = rh.CreatedAt
                        })
                        .ToList()
                })
                .ToListAsync();

            // Get total count before pagination
            var totalCount = applicationsData.Count;

            // Apply pagination
            var paginatedData = applicationsData
                .Skip((pagination.PageNumber - 1) * pagination.PageSize)
                .Take(pagination.PageSize)
                .ToList();

            // Map to ApplicationDetailsDto
            var detailedApplications = paginatedData.Select(a => new ApplicationDetailsDto
            {
                Id = a.Id,
                ApplicationNumber = a.ApplicationNumber,
                Status = a.Status,
                Priority = a.Priority,
                FirstName = a.FirstName,
                MiddleName = a.MiddleName,
                LastName = a.LastName,
                PhoneNumber = a.PhoneNumber,
                RsaPin = a.RsaPin,
                EquityValue = a.EquityValue,
                SupplementaryValue = a.SupplementaryValue,
                LoanAmount = a.LoanAmount,
                PropertyValue = a.PropertyValue,
                PmiCompanyId = a.PmiCompanyId,
                PmiCompanyName = a.PmiCompanyName,
                PmiRepName = a.PmiRepName,
                PmiRepEmail = a.PmiRepEmail,
                UserId = a.UserId,
                UserName = $"{a.UserFirstName} {a.UserLastName}".Trim(),
                UserEmail = a.UserEmail,
                DocumentCount = a.DocumentCount,
                Documents = a.Documents,
                CreatedAt = a.CreatedAt,
                SubmittedAt = a.SubmittedAt,
                UpdatedAt = a.UpdatedAt,
                ReviewHistory = a.ReviewHistory
            }).ToList();

            return new PaginatedResponse<ApplicationDetailsDto>
            {
                Data = detailedApplications,
                TotalCount = totalCount,
                PageNumber = pagination.PageNumber,
                PageSize = pagination.PageSize
            };
        }

        public async Task<ClientApplicationSummaryDto> GetApplicationsSummaryByUserAsync(Guid userId)
        {
            // Get user's applications with all statuses (including draft)
            var applicationsData = await _context.Applications
                .AsNoTracking()
                .Where(a => a.UserId == userId)
                .Select(a => new
                {
                    a.Status,
                    a.LoanAmount,
                    a.PropertyValue
                })
                .ToListAsync();

            // Count total records
            var totalCount = applicationsData.Count;

            // Group by status and count
            var statusCounts = applicationsData
                .GroupBy(a => a.Status.ToLower())
                .Select(g => new { Status = g.Key, Count = g.Count() })
                .ToList();

            // Calculate totals
            var totalLoanAmount = applicationsData.Sum(a => a.LoanAmount);
            var totalPropertyValue = applicationsData.Sum(a => a.PropertyValue);

            var summary = new ClientApplicationSummaryDto
            {
                TotalRecords = totalCount,
                DraftCount = statusCounts.FirstOrDefault(s => s.Status == "draft")?.Count ?? 0,
                SubmittedCount = statusCounts.FirstOrDefault(s => s.Status == "submitted")?.Count ?? 0,
                UnderReviewCount = statusCounts.FirstOrDefault(s => s.Status == "under_review")?.Count ?? 0,
                PendingCount = statusCounts.FirstOrDefault(s => s.Status == "pending")?.Count ?? 0,
                RejectedCount = statusCounts.FirstOrDefault(s => s.Status == "rejected")?.Count ?? 0,
                ReturnedCount = statusCounts.FirstOrDefault(s => s.Status == "returned")?.Count ?? 0,
                ApprovedCount = statusCounts.FirstOrDefault(s => s.Status == "approved")?.Count ?? 0,
                CompletedCount = statusCounts.FirstOrDefault(s => s.Status == "completed")?.Count ?? 0,
                TotalLoanAmount = totalLoanAmount,
                TotalPropertyValue = totalPropertyValue
            };

            return summary;
        }

        public async Task<List<ApplicationDto>> GetApplicationsByPMIAsync(Guid pmiCompanyId)
        {
            return await GetApplicationsQueryExcludingDraft()
                .Where(a => a.PmiCompanyId == pmiCompanyId)
                .ToListAsync();
        }

        public async Task<PaginatedResponse<ApplicationDto>> GetApplicationsByPMIPagedAsync(Guid pmiCompanyId, PaginationRequest pagination)
        {
            pagination.Validate();

            var query = GetApplicationsQueryExcludingDraft()
                .Where(a => a.PmiCompanyId == pmiCompanyId);

            // Execute count first, then data - sequentially to avoid DbContext threading issues
            var totalCount = await query.CountAsync();
            var data = await query
                .Skip((pagination.PageNumber - 1) * pagination.PageSize)
                .Take(pagination.PageSize)
                .ToListAsync();

            return new PaginatedResponse<ApplicationDto>
            {
                Data = data,
                TotalCount = totalCount,
                PageNumber = pagination.PageNumber,
                PageSize = pagination.PageSize
            };
        }

        public async Task<ApplicationDto> GetApplicationByIdAsync(Guid id)
        {
            var application = await _context.Applications
                .AsNoTracking()
                .Select(a => new ApplicationDto
                {
                    Id = a.Id,
                    ApplicationNumber = a.ApplicationNumber,
                    Status = a.Status,
                    Priority = a.Priority,
                    FirstName = a.FirstName,
                    MiddleName = a.MiddleName,
                    LastName = a.LastName,
                    PhoneNumber = a.PhoneNumber,
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
                .FirstOrDefaultAsync(a => a.Id == id);

            if (application == null)
                throw new ResourceNotFoundException("Application", id.ToString());

            return application;
        }

        // ===== NEW: Check if RsaPin already exists (NEVER allow reuse, even if completed/approved) =====
        public async Task<bool> CheckRsaPinExistsAsync(string rsaPin)
        {
            if (string.IsNullOrWhiteSpace(rsaPin))
                return false;

            // Check ALL applications - RsaPin can NEVER be reused
            return await _context.Applications
                .Where(a => a.RsaPin == rsaPin)
                .AnyAsync();
        }

        // ===== NEW: Get existing application by RsaPin (NEVER reused, checks all statuses) =====
        public async Task<ApplicationDto?> GetApplicationByRsaPinAsync(string rsaPin)
        {
            if (string.IsNullOrWhiteSpace(rsaPin))
                return null;

            // Retrieve application regardless of status - RsaPin can NEVER be reused
            return await _context.Applications
                .AsNoTracking()
                .Where(a => a.RsaPin == rsaPin)
                .Select(a => new ApplicationDto
                {
                    Id = a.Id,
                    ApplicationNumber = a.ApplicationNumber,
                    Status = a.Status,
                    FirstName = a.FirstName,
                    LastName = a.LastName,
                    RsaPin = a.RsaPin,
                    CreatedAt = a.CreatedAt,
                    UserId = a.UserId
                })
                .FirstOrDefaultAsync();
        }
        // ===== END NEW METHODS =====

        public async Task<ApplicationDto> CreateApplicationAsync(Guid userId, CreateApplicationRequest request)
        {
            try
            {
                // ===== NEW: Validate RsaPin is not already used in active application =====
                var existingApp = await GetApplicationByRsaPinAsync(request.RsaPin);
                if (existingApp != null)
                {
                    throw new BusinessRuleException(
                        $"An active application already exists with RSA PIN '{request.RsaPin}'. " +
                        $"Application Number: {existingApp.ApplicationNumber}. " +
                        $"Please use a different RSA PIN or contact support.");
                }
                // ===== END NEW VALIDATION =====

                var user = await _context.Users
                    .Include(u => u.PMICompany)
                    .FirstOrDefaultAsync(u => u.Id == userId);

                if (user == null)
                    throw new ResourceNotFoundException("User", userId.ToString());

                if (user.PMICompany == null)
                    throw new BusinessRuleException("Your user account is not associated with a PMI company. Please contact your administrator to link your account.");

                var application = new Application
                {
                    Id = Guid.NewGuid(),
                    ApplicationNumber = GenerateApplicationNumber(),
                    Status = "draft",
                    Priority = "medium",
                    FirstName = request.FirstName,
                    MiddleName = request.MiddleName,
                    LastName = request.LastName,
                    PhoneNumber = request.PhoneNumber,
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
                catch (ExternalServiceException)
                {
                    // Email failure should not prevent application creation
                }

                return await GetApplicationByIdAsync(application.Id);
            }
            catch (DbUpdateException ex)
            {
                var innerException = ex.InnerException?.Message ?? ex.Message;
                throw new DatabaseException($"Failed to save application to database: {innerException}");
            }
            catch (Exception ex) when (!(ex is MMGT.API.Exceptions.ApplicationException))
            {
                throw new DatabaseException($"Failed to create application: {ex.Message}");
            }
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

            // ===== NEW: Set CompletedAt for terminal statuses (performance tracking) =====
            // When an application reaches a terminal status, record the completion time
            // so that performance metrics can calculate turnaround hours: CompletedAt - AssignedAt
            var normalizedNewStatus = request.Status.ToLower().Trim();
            if (normalizedNewStatus == "rejected" || normalizedNewStatus == "approved" || 
                normalizedNewStatus == "returned" || normalizedNewStatus == "completed")
            {
                if (!application.CompletedAt.HasValue)
                {
                    application.CompletedAt = DateTime.UtcNow;
                }
            }
            // ===== END NEW: CompletedAt for terminal statuses =====

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

            // ===== NEW: Send SMS notification to borrower on status change =====
            try
            {
                string? smsMessage = null;
                var normalizedStatus = request.Status.ToLower().Trim();

                _logger.LogInformation("[SMS-DEBUG] Processing SMS for Application {ApplicationNumber}, Status: '{RawStatus}' → Normalized: '{NormalizedStatus}', PhoneNumber: '{PhoneNumber}'",
                    application.ApplicationNumber, request.Status, normalizedStatus, 
                    string.IsNullOrWhiteSpace(application.PhoneNumber) ? "(empty)" : "***hidden***");

                // NOTE: SMS is disabled for "approved" status only
                if (normalizedStatus == "rejected")
                {
                    smsMessage = "Dear Client, your Mortgage application was declined due to incomplete documentation and has informed your PMI for update";
                    _logger.LogInformation("[SMS-DEBUG] Matched status 'rejected' - SMS message prepared for {ApplicationNumber}", application.ApplicationNumber);
                }
                else if (normalizedStatus == "resubmitted")
                {
                    smsMessage = "Dear Client, your Mortgage application has been resubmitted and is currently being Reviewed";
                    _logger.LogInformation("[SMS-DEBUG] Matched status 'resubmitted' - SMS message prepared for {ApplicationNumber}", application.ApplicationNumber);
                }
                else
                {
                    _logger.LogInformation("[SMS-DEBUG] Status '{NormalizedStatus}' does not match any SMS trigger (rejected/resubmitted only). No SMS will be sent for {ApplicationNumber}", 
                        normalizedStatus, application.ApplicationNumber);
                }

                // Send SMS if message is applicable and phone number exists
                if (smsMessage != null)
                {
                    if (string.IsNullOrWhiteSpace(application.PhoneNumber))
                    {
                        _logger.LogWarning("[SMS-WARN] SMS message prepared but NO PHONE NUMBER for application {ApplicationNumber}. Phone value: '{PhoneValue}'",
                            application.ApplicationNumber, application.PhoneNumber ?? "(null)");
                    }
                    else
                    {
                        _logger.LogInformation("[SMS-INFO] Sending SMS to {ApplicationNumber}. Phone: {Phone}, Message length: {MsgLen}",
                            application.ApplicationNumber, "***hidden***", smsMessage.Length);

                        var smsResult = await _smsService.SendSmsAsync(application.PhoneNumber, smsMessage, application.ApplicationNumber);

                        if (smsResult)
                        {
                            _logger.LogInformation("[SMS-SUCCESS] SMS sent successfully for {ApplicationNumber}", application.ApplicationNumber);
                        }
                        else
                        {
                            _logger.LogWarning("[SMS-FAILED] SMS service returned false for {ApplicationNumber}", application.ApplicationNumber);
                        }
                    }
                }
                else
                {
                    _logger.LogInformation("[SMS-DEBUG] No SMS message for application {ApplicationNumber} - status does not trigger SMS", application.ApplicationNumber);
                }
            }
            catch (Exception ex)
            {
                // Log SMS error but don't prevent status update
                _logger.LogError(ex, "[SMS-ERROR] Exception while sending SMS notification for application {ApplicationNumber} with status {Status}. Exception: {ExMessage}",
                    application.ApplicationNumber, request.Status, ex.Message);
            }
            // ===== END NEW: SMS STATUS UPDATE =====

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
            app.PhoneNumber = request.PhoneNumber ?? app.PhoneNumber;
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

            // VALIDATION: Check that all 21 mandatory documents have been uploaded
            var (isComplete, missingDocuments) = await ValidateMandatoryDocumentsAsync(id);
            if (!isComplete)
            {
                var missingDocNames = string.Join(", ", missingDocuments.Select(d => $"'{d.Name}'"));
                throw new Exception($"Cannot submit application. Missing {missingDocuments.Count} mandatory document(s): {missingDocNames}. All 21 mandatory documents must be uploaded before submission.");
            }

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
            application.MandatoryDocumentsComplete = true; // Set to true when submitted with all docs

            await _context.SaveChangesAsync();

            // Auto-assign the application to a reviewer if it's newly submitted (not a returned resubmission)
            if (previousStatus == "draft")
            {
                try
                {
                    _logger.LogInformation($"[AUTO-ASSIGN] Starting auto-assignment for application {id}, Status: {application.Status}, SubmittedAt: {application.SubmittedAt}");
                    await _assignmentService.AutoAssignApplicationAsync(id);
                    _logger.LogInformation($"[AUTO-ASSIGN] ✓ Successfully auto-assigned application {id}");
                }
                catch (Exception ex)
                {
                    // Log the assignment failure but don't prevent application submission
                    // The application is still valid even if auto-assignment fails;
                    // manual assignment can be done later by admin
                    _logger.LogError(ex, $"[AUTO-ASSIGN] ✗ FAILED to auto-assign application {id}. Exception Type: {ex.GetType().Name}, Message: {ex.Message}, StackTrace: {ex.StackTrace}");

                    // DIAGNOSTIC: Log the current database state
                    try
                    {
                        var assignmentCount = await _context.ApplicationAssignments
                            .Where(a => a.ApplicationId == id && a.IsActive)
                            .CountAsync();
                        _logger.LogError($"[AUTO-ASSIGN] Current active assignments for app {id}: {assignmentCount}");
                    }
                    catch (Exception diagEx)
                    {
                        _logger.LogError($"[AUTO-ASSIGN] Failed to log diagnostic info: {diagEx.Message}");
                    }
                }
            }

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

            // ===== NEW: Send SMS acknowledgement to borrower =====
            try
            {
                string? smsMessage = null;

                // SMS message depends on whether this is an initial submission or a returned resubmission
                if (previousStatus == "draft")
                {
                    // Initial submission from draft
                    smsMessage = "Dear Client, your mortgage application has been received and is currently under review. Access Pensions.";
                    _logger.LogInformation("[SMS-SUBMIT] Sending initial submission SMS for application {ApplicationNumber}", application.ApplicationNumber);
                }
                else if (previousStatus == "rejected" || previousStatus == "resubmitted")
                {
                    // Resubmission after rejection or when status was marked as resubmitted
                    smsMessage = "Dear Client, your updated mortgage application has been received and is currently under review.";
                    _logger.LogInformation("[SMS-RESUBMIT] Sending resubmission SMS for application {ApplicationNumber}. PreviousStatus: {PreviousStatus} → NewStatus: returned",
                        application.ApplicationNumber, previousStatus);
                }

                if (!string.IsNullOrWhiteSpace(smsMessage) && !string.IsNullOrWhiteSpace(application.PhoneNumber))
                {
                    _logger.LogInformation("[SMS-SUBMIT-SEND] Sending SMS to {ApplicationNumber}. Message type: {Type}",
                        application.ApplicationNumber, previousStatus == "draft" ? "initial-submission" : "resubmission");

                    var smsResult = await _smsService.SendSmsAsync(application.PhoneNumber, smsMessage, application.ApplicationNumber);

                    if (smsResult)
                    {
                        _logger.LogInformation("[SMS-SUBMIT-SUCCESS] SMS delivery queued successfully for application {ApplicationNumber}", application.ApplicationNumber);
                    }
                    else
                    {
                        _logger.LogWarning("[SMS-SUBMIT-FAILED] SMS service returned false for application {ApplicationNumber}", application.ApplicationNumber);
                    }
                }
                else if (string.IsNullOrWhiteSpace(application.PhoneNumber))
                {
                    _logger.LogWarning("[SMS-SUBMIT-WARN] SMS message prepared but NO PHONE NUMBER for application {ApplicationNumber}. Phone value: '{PhoneValue}'",
                        application.ApplicationNumber, application.PhoneNumber ?? "(null)");
                }
            }
            catch (Exception ex)
            {
                // Log SMS error but don't prevent application submission
                _logger.LogError(ex, "[SMS-SUBMIT-ERROR] Exception while sending SMS acknowledgement for application {ApplicationNumber}. Exception: {ExMessage}",
                    application.ApplicationNumber, ex.Message);
            }
            // ===== END NEW: SMS ACKNOWLEDGEMENT & RESUBMISSION =====

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
                .AsNoTracking()
                .Include(rh => rh.ReviewedBy)
                .Where(rh => rh.ApplicationId == applicationId)
                .OrderByDescending(rh => rh.CreatedAt)
                .Select(rh => new ReviewHistoryDto
                {
                    Id = rh.Id,
                    Status = rh.Status,
                    Comments = rh.Comments,
                    CreatedAt = rh.CreatedAt,
                    ReviewedById = rh.ReviewedById,
                    ReviewedByName = $"{rh.ReviewedBy.FirstName} {rh.ReviewedBy.LastName}",
                    ReviewedByEmail = rh.ReviewedBy.Email
                })
                .ToListAsync();

            return reviewHistory;
        }

        public async Task<PaginatedResponse<ReviewHistoryDto>> GetApplicationReviewHistoryPagedAsync(Guid applicationId, PaginationRequest pagination)
        {
            pagination.Validate();

            var query = _context.ApplicationReviewHistory
                .AsNoTracking()
                .Include(rh => rh.ReviewedBy)
                .Where(rh => rh.ApplicationId == applicationId)
                .OrderByDescending(rh => rh.CreatedAt);

            var totalCount = await query.CountAsync();

            var reviewHistory = await query
                .Skip((pagination.PageNumber - 1) * pagination.PageSize)
                .Take(pagination.PageSize)
                .Select(rh => new ReviewHistoryDto
                {
                    Id = rh.Id,
                    Status = rh.Status,
                    Comments = rh.Comments,
                    CreatedAt = rh.CreatedAt,
                    ReviewedById = rh.ReviewedById,
                    ReviewedByName = $"{rh.ReviewedBy.FirstName} {rh.ReviewedBy.LastName}",
                    ReviewedByEmail = rh.ReviewedBy.Email
                })
                .ToListAsync();

            return new PaginatedResponse<ReviewHistoryDto>
            {
                Data = reviewHistory,
                TotalCount = totalCount,
                PageNumber = pagination.PageNumber,
                PageSize = pagination.PageSize
            };
        }

        public async Task<PaginatedResponse<DTOs.Assignment.AssignmentHistoryDto>> GetApplicationAssignmentHistoryPagedAsync(Guid applicationId, PaginationRequest pagination)
        {
            pagination.Validate();

            var query = _context.ApplicationAssignments
                .AsNoTracking()
                .Include(aa => aa.Reviewer)
                .Include(aa => aa.AssignedByUser)
                .Where(aa => aa.ApplicationId == applicationId)
                .OrderBy(aa => aa.AssignedAt); // Chronological order (oldest first)

            var totalCount = await query.CountAsync();

            var assignments = await query
                .Skip((pagination.PageNumber - 1) * pagination.PageSize)
                .Take(pagination.PageSize)
                .Select(aa => new DTOs.Assignment.AssignmentHistoryDto
                {
                    Id = aa.Id,
                    ApplicationId = aa.ApplicationId,
                    ReviewerId = aa.ReviewerId,
                    ReviewerName = $"{aa.Reviewer.FirstName} {aa.Reviewer.LastName}",
                    ReviewerEmail = aa.Reviewer.Email,
                    PreviousReviewerName = null, // We get current reviewer, not tracking history of previous
                    PreviousReviewerEmail = null,
                    AssignedBy = aa.AssignedBy,
                    AssignedByName = aa.AssignedBy == null ? "System" : $"{aa.AssignedByUser.FirstName} {aa.AssignedByUser.LastName}",
                    AssignedAt = aa.AssignedAt,
                    ReassignmentReason = aa.ReassignmentReason,
                    IsActive = aa.IsActive
                })
                .ToListAsync();

            return new PaginatedResponse<DTOs.Assignment.AssignmentHistoryDto>
            {
                Data = assignments,
                TotalCount = totalCount,
                PageNumber = pagination.PageNumber,
                PageSize = pagination.PageSize
            };
        }

        public async Task<List<DTOs.Assignment.AssignmentHistoryDto>> GetApplicationAssignmentHistoryAsync(Guid applicationId)
        {
            var assignments = await _context.ApplicationAssignments
                .AsNoTracking()
                .Include(aa => aa.Reviewer)
                .Include(aa => aa.AssignedByUser)
                .Where(aa => aa.ApplicationId == applicationId)
                .OrderBy(aa => aa.AssignedAt) // Chronological order (oldest first)
                .Select(aa => new DTOs.Assignment.AssignmentHistoryDto
                {
                    Id = aa.Id,
                    ApplicationId = aa.ApplicationId,
                    ReviewerId = aa.ReviewerId,
                    ReviewerName = $"{aa.Reviewer.FirstName} {aa.Reviewer.LastName}",
                    ReviewerEmail = aa.Reviewer.Email,
                    PreviousReviewerName = null,
                    PreviousReviewerEmail = null,
                    AssignedBy = aa.AssignedBy,
                    AssignedByName = aa.AssignedBy == null ? "System" : $"{aa.AssignedByUser.FirstName} {aa.AssignedByUser.LastName}",
                    AssignedAt = aa.AssignedAt,
                    ReassignmentReason = aa.ReassignmentReason,
                    IsActive = aa.IsActive
                })
                .ToListAsync();

            return assignments;
        }

        public async Task<List<ApplicationDto>> SearchApplicationsAsync(string searchTerm)
        {
            if (string.IsNullOrWhiteSpace(searchTerm))
                return new List<ApplicationDto>();

            var searchPattern = $"%{searchTerm}%";

            return await _context.Applications
                .AsNoTracking()
                .Where(a => EF.Functions.Like(a.FirstName, searchPattern) ||
                            EF.Functions.Like(a.LastName, searchPattern) ||
                            EF.Functions.Like(a.MiddleName, searchPattern) ||
                            EF.Functions.Like(a.RsaPin, searchPattern) ||
                            EF.Functions.Like(a.ApplicationNumber, searchPattern))
                .OrderByDescending(a => a.CreatedAt)
                .Take(20)
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
        }

        public async Task<PaginatedResponse<ApplicationSearchDto>> SearchApplicationsAdvancedAsync(
            string? rsaPin = null,
            string? borrowerName = null,
            DateTime? submittedFromDate = null,
            DateTime? submittedToDate = null,
            int pageNumber = 1,
            int pageSize = 50)
        {
            // Validate pagination
            if (pageNumber < 1) pageNumber = 1;
            if (pageSize < 1 || pageSize > 500) pageSize = 50;

            // Start with base query - include all applications (not excluding draft)
            var query = _context.Applications
                .AsNoTracking()
                .Include(a => a.User)
                .Include(a => a.PmiCompany)
                .Include(a => a.ReviewHistory)
                .AsQueryable();

            // Apply RSA PIN filter (exact or partial match, case-insensitive)
            if (!string.IsNullOrWhiteSpace(rsaPin))
            {
                var rsaPinPattern = $"%{rsaPin.Trim()}%";
                query = query.Where(a => EF.Functions.Like(a.RsaPin, rsaPinPattern));
            }

            // Apply Borrower Name filter (FirstName and/or LastName, case-insensitive)
            if (!string.IsNullOrWhiteSpace(borrowerName))
            {
                var namePattern = $"%{borrowerName.Trim()}%";
                query = query.Where(a =>
                    EF.Functions.Like(a.FirstName, namePattern) ||
                    EF.Functions.Like(a.LastName, namePattern) ||
                    EF.Functions.Like(a.MiddleName, namePattern));
            }

            // Apply SubmittedAt date range filter
            if (submittedFromDate.HasValue)
            {
                query = query.Where(a => a.SubmittedAt >= submittedFromDate);
            }

            if (submittedToDate.HasValue)
            {
                // Add 1 day to include all records submitted on the "to" date
                var toDateEndOfDay = submittedToDate.Value.AddDays(1);
                query = query.Where(a => a.SubmittedAt < toDateEndOfDay);
            }

            // Order by most recent first
            query = query.OrderByDescending(a => a.SubmittedAt ?? a.CreatedAt);

            // Get total count before pagination
            var totalCount = await query.CountAsync();

            // Apply pagination
            var applications = await query
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            // Project to ApplicationSearchDto with latest review comment
            var searchResults = applications.Select(a => new ApplicationSearchDto
            {
                Id = a.Id,
                ApplicationNumber = a.ApplicationNumber,
                Status = a.Status,
                FirstName = a.FirstName,
                MiddleName = a.MiddleName,
                LastName = a.LastName,
                PhoneNumber = a.PhoneNumber,
                RsaPin = a.RsaPin,
                PropertyValue = a.PropertyValue,
                LoanAmount = a.LoanAmount,
                EquityValue = a.EquityValue,
                SupplementaryValue = a.SupplementaryValue,
                PmiCompanyName = a.PmiCompany?.PmiName,
                PmiRepName = a.PmiRepName,
                PmiRepEmail = a.PmiRepEmail,
                UserId = a.UserId,
                UserName = $"{a.User?.FirstName} {a.User?.LastName}".Trim(),
                DocumentCount = a.Documents.Count,
                CreatedAt = a.CreatedAt,
                SubmittedAt = a.SubmittedAt,
                UpdatedAt = a.UpdatedAt,
                LatestReviewComment = a.ReviewHistory.OrderByDescending(rh => rh.CreatedAt).FirstOrDefault()?.Comments,
                LatestReviewerName = a.ReviewHistory.OrderByDescending(rh => rh.CreatedAt).FirstOrDefault() != null
                    ? $"{a.ReviewHistory.OrderByDescending(rh => rh.CreatedAt).FirstOrDefault()?.ReviewedBy?.FirstName} {a.ReviewHistory.OrderByDescending(rh => rh.CreatedAt).FirstOrDefault()?.ReviewedBy?.LastName}".Trim()
                    : null,
                LatestReviewDate = a.ReviewHistory.OrderByDescending(rh => rh.CreatedAt).FirstOrDefault()?.CreatedAt
            }).ToList();

            return new PaginatedResponse<ApplicationSearchDto>
            {
                Data = searchResults,
                TotalCount = totalCount,
                PageNumber = pageNumber,
                PageSize = pageSize
            };
        }

        /// <summary>
        /// OPTIMIZED: Get complete application details with review history, assignments, and documents.
        /// Uses single query with projections instead of multiple includes and in-memory LINQ operations.
        /// Excludes FileContent BLOB from document queries for better performance.
        /// </summary>
        public async Task<ApplicationDetailsDto> GetApplicationDetailsAsync(Guid applicationId)
        {
            var application = await _context.Applications
                .AsNoTracking()
                .Where(a => a.Id == applicationId)
                .Select(a => new
                {
                    // Application properties
                    a.Id,
                    a.ApplicationNumber,
                    a.Status,
                    a.Priority,
                    a.FirstName,
                    a.MiddleName,
                    a.LastName,
                    a.PhoneNumber,
                    a.RsaPin,
                    a.EquityValue,
                    a.SupplementaryValue,
                    a.LoanAmount,
                    a.PropertyValue,
                    a.PmiCompanyId,
                    a.PmiCompanyName,
                    a.PmiRepName,
                    a.PmiRepEmail,
                    a.UserId,
                    a.CreatedAt,
                    a.SubmittedAt,
                    a.UpdatedAt,
                    // User info
                    UserFirstName = a.User!.FirstName,
                    UserLastName = a.User!.LastName,
                    UserEmail = a.User!.Email,
                    // PMI info
                    PmiName = a.PmiCompany!.PmiName,
                    DocumentCount = a.Documents.Count,
                    // OPTIMIZED: Documents projection - EXCLUDES FileContent BLOB
                    Documents = a.Documents
                        .OrderByDescending(d => d.CreatedAt)
                        .Select(d => new ApplicationDocumentDetailDto
                        {
                            Id = d.Id,
                            DocumentType = d.DocumentType!.Name ?? "Unknown",
                            FileName = d.FileName,
                            ReviewStatus = d.ReviewStatus,
                            ReviewerComments = d.ReviewComments,
                            ReviewerName = d.Reviewer != null
                                ? $"{d.Reviewer.FirstName} {d.Reviewer.LastName}"
                                : null,
                            ReviewedAt = d.ReviewedAt,
                            UploadedAt = d.CreatedAt
                        })
                        .ToList(),
                    // Review history projection - ordered in query, not in memory
                    ReviewHistory = a.ReviewHistory
                        .OrderByDescending(rh => rh.CreatedAt)
                        .Select(rh => new ReviewHistoryDetailDto
                        {
                            Id = rh.Id,
                            Status = rh.Status,
                            UpdatedStatus = rh.Status,
                            ReviewComments = rh.Comments,
                            ReviewerName = $"{rh.ReviewedBy.FirstName} {rh.ReviewedBy.LastName}".Trim(),
                            ReviewerEmail = rh.ReviewedBy.Email,
                            ReviewedAt = rh.CreatedAt
                        })
                        .ToList(),
                    // Current active assignment
                    CurrentAssignment = a.Assignments
                        .Where(aa => aa.IsActive)
                        .OrderByDescending(aa => aa.AssignedAt)
                        .Select(aa => new ApplicationAssignmentDetailDto
                        {
                            Id = aa.Id,
                            ReviewerName = $"{aa.Reviewer.FirstName} {aa.Reviewer.LastName}".Trim(),
                            ReviewerEmail = aa.Reviewer.Email,
                            ReassignmentReason = aa.ReassignmentReason,
                            AssignedAt = aa.AssignedAt,
                            IsActive = aa.IsActive
                        })
                        .FirstOrDefault(),
                    // Assignment history
                    AssignmentHistory = a.Assignments
                        .OrderByDescending(aa => aa.AssignedAt)
                        .Select(aa => new ApplicationAssignmentDetailDto
                        {
                            Id = aa.Id,
                            ReviewerName = $"{aa.Reviewer.FirstName} {aa.Reviewer.LastName}".Trim(),
                            ReviewerEmail = aa.Reviewer.Email,
                            ReassignmentReason = aa.ReassignmentReason,
                            AssignedAt = aa.AssignedAt,
                            IsActive = aa.IsActive
                        })
                        .ToList()
                })
                .FirstOrDefaultAsync();

            if (application == null)
                throw new Exceptions.ApplicationException($"Application {applicationId} not found");

            return new ApplicationDetailsDto
            {
                Id = application.Id,
                ApplicationNumber = application.ApplicationNumber,
                Status = application.Status,
                Priority = application.Priority,
                FirstName = application.FirstName,
                MiddleName = application.MiddleName,
                LastName = application.LastName,
                PhoneNumber = application.PhoneNumber,
                RsaPin = application.RsaPin,
                EquityValue = application.EquityValue,
                SupplementaryValue = application.SupplementaryValue,
                LoanAmount = application.LoanAmount,
                PropertyValue = application.PropertyValue,
                PmiCompanyId = application.PmiCompanyId,
                PmiCompanyName = application.PmiName,
                PmiRepName = application.PmiRepName,
                PmiRepEmail = application.PmiRepEmail,
                UserId = application.UserId,
                UserName = $"{application.UserFirstName} {application.UserLastName}".Trim(),
                UserEmail = application.UserEmail ?? string.Empty,
                DocumentCount = application.DocumentCount,
                Documents = application.Documents,
                CreatedAt = application.CreatedAt,
                SubmittedAt = application.SubmittedAt,
                UpdatedAt = application.UpdatedAt,
                ReviewHistory = application.ReviewHistory,
                CurrentAssignment = application.CurrentAssignment,
                AssignmentHistory = application.AssignmentHistory
            };
        }

        /// <summary>
        /// Validates that all 21 mandatory documents have been uploaded for the application.
        /// </summary>
        /// <param name="applicationId">The application ID to validate</param>
        /// <returns>Tuple of (IsComplete, MissingDocuments)</returns>
        public async Task<(bool IsComplete, List<DocumentType> MissingDocuments)> ValidateMandatoryDocumentsAsync(Guid applicationId)
        {
            // Get all mandatory document types (21 total)
            var mandatoryDocTypes = await _context.DocumentTypes
                .Where(dt => dt.IsMandatory && dt.IsActive)
                .ToListAsync();

            // Get uploaded documents for this application
            var uploadedDocuments = await _context.ApplicationDocuments
                .Where(ad => ad.ApplicationId == applicationId)
                .Select(ad => ad.DocumentTypeId)
                .Distinct()
                .ToListAsync();

            // Find missing mandatory documents
            var missingDocTypes = mandatoryDocTypes
                .Where(dt => !uploadedDocuments.Contains(dt.Id))
                .ToList();

            bool isComplete = missingDocTypes.Count == 0;

            return (isComplete, missingDocTypes);
        }
    }
}