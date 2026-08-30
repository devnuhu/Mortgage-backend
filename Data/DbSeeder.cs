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
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
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

            // Create PMI Role User (with full CLIENT capabilities)
            var pmiRoleUser = new User
            {
                Id = Guid.NewGuid(),
                Email = "pmirole@company.com",
                FirstName = "PMI",
                LastName = "Representative",
                Role = "PMI",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("pmi123"),
                IsFirstLogin = false,
                IsActive = true,
                PMICompanyId = pmiCompany.Id,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            // Create SERVICE_MANAGER User
            var serviceManager = new User
            {
                Id = Guid.NewGuid(),
                Email = "servicemanager@pfa.com",
                FirstName = "Service",
                LastName = "Manager",
                Role = "SERVICE_MANAGER",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("manager123"),
                IsFirstLogin = false,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            // Add to context
            context.Users.AddRange(superAdmin, admin, pmiClient, pmiRoleUser, serviceManager);
            context.PMICompanies.Add(pmiCompany);

            // Save changes
            await context.SaveChangesAsync();

            // Seed default DocumentTypes - ALWAYS ensure we have exactly 24 documents
            var existingDocTypes = await context.DocumentTypes.ToListAsync();
            int mandatoryCount = existingDocTypes.Count(dt => dt.IsMandatory);

            // Check if we need to reseed
            // Force reseed if:
            // 1. Count is not exactly 24, OR
            // 2. Mandatory count is not exactly 21, OR
            // 3. Any of the expected document codes are missing
            var expectedCodes = new[] { "ACCESS_ARM_APPLICATION_FORM", "LETTER_OF_REQUEST_TO_ACCESS_ARM", "LETTER_FROM_EMPLOYER_RETIREMENT_DATE", 
                "MEANS_OF_IDENTIFICATION", "BANK_ACCOUNT_DETAILS", "MORTGAGE_LOAN_OFFER_LETTER", "PROPERTY_OFFER_LETTER", 
                "VERIFICATION_OF_PROPERTY_LETTER", "CONSENT_LETTER_RSA_HOLDER", "INDEMNITY_FORM_APPLICANTS", 
                "INDEMNITY_FORM_MORTGAGE_LENDER", "PENSION_COMPLIANCE_CERTIFICATE", "RSA_STATEMENT_AND_TRANSFER_HISTORY", 
                "PROPERTY_VALUATION_REPORT", "PROPERTY_INSURANCE", "CREDIT_LIFE_INSURANCE", "CONFIRMATION_OF_AVAILABILITY", 
                "PROPERTY_TITLE_DOCUMENT", "CONFIRMATION_OF_TITLE", "READINESS_TO_DISBURSE", "APPLICATION_FORM_TO_MORTGAGE_LENDER", 
                "MARRIAGE_CERTIFICATE", "CONSENT_LETTER_LOSS_JOB", "PASSPORT_PHOTOGRAPH" };

            var existingCodes = existingDocTypes.Select(dt => dt.Code).ToHashSet();
            bool missingExpectedDocs = expectedCodes.Any(code => !existingCodes.Contains(code));

            bool needsReseed = existingDocTypes.Count != 24 || mandatoryCount != 21 || missingExpectedDocs;

            Console.WriteLine($"[DbSeeder] DocumentTypes check: Count={existingDocTypes.Count}, MandatoryCount={mandatoryCount}, MissingExpectedDocs={missingExpectedDocs}, NeedsReseed={needsReseed}");

            if (needsReseed)
            {
                Console.WriteLine($"[DbSeeder] Starting DocumentType reseed - deleting {existingDocTypes.Count} old documents");

                // First, delete all ApplicationDocuments that reference the old DocumentTypes
                // This is necessary because ApplicationDocument has OnDelete(DeleteBehavior.Restrict) on DocumentType
                var oldDocTypeIds = existingDocTypes.Select(dt => dt.Id).ToList();
                var applicationsDocsToDelete = await context.ApplicationDocuments                    
                    .Where(ad => oldDocTypeIds.Contains(ad.DocumentTypeId))
                    .ToListAsync();

                if (applicationsDocsToDelete.Any())
                {
                    Console.WriteLine($"[DbSeeder] Deleting {applicationsDocsToDelete.Count} ApplicationDocuments that reference old DocumentTypes");
                    context.ApplicationDocuments.RemoveRange(applicationsDocsToDelete);
                    await context.SaveChangesAsync();
                    Console.WriteLine($"[DbSeeder] ApplicationDocuments deleted");
                }

                // Now delete all existing document types to start fresh
                Console.WriteLine($"[DbSeeder] Deleting {existingDocTypes.Count} old DocumentTypes");
                foreach (var docType in existingDocTypes)
                {
                    context.DocumentTypes.Remove(docType);
                }
                await context.SaveChangesAsync();
                Console.WriteLine($"[DbSeeder] Old DocumentTypes deleted");

                var defaultDocTypes = new List<DocumentType>
                {
                    // Mandatory Documents (21)
                    new DocumentType { Id = Guid.NewGuid(), Code = "ACCESS_ARM_APPLICATION_FORM", Name = "ACCESS-ARM APPLICATION FORM (PROPERLY SIGNED)", IsMandatory = true, DisplayOrder = 1, MaxFileSizeKB = 700, IsActive = true, CreatedAt = DateTime.UtcNow, CreatedBy = superAdmin.Id },
                    new DocumentType { Id = Guid.NewGuid(), Code = "LETTER_OF_REQUEST_TO_ACCESS_ARM", Name = "LETTER OF REQUEST TO ACCESS PENSIONS", IsMandatory = true, DisplayOrder = 2, MaxFileSizeKB = 700, IsActive = true, CreatedAt = DateTime.UtcNow, CreatedBy = superAdmin.Id },
                    new DocumentType { Id = Guid.NewGuid(), Code = "LETTER_FROM_EMPLOYER_RETIREMENT_DATE", Name = "LETTER FROM EMPLOYER STATING THE DATE OF RETIREMENT", IsMandatory = true, DisplayOrder = 3, MaxFileSizeKB = 700, IsActive = true, CreatedAt = DateTime.UtcNow, CreatedBy = superAdmin.Id },
                    new DocumentType { Id = Guid.NewGuid(), Code = "MEANS_OF_IDENTIFICATION", Name = "MEANS OF IDENTIFICATION FROM THE RSA HOLDER, SAME FOR JOINT MORTGAGE WHICH IS NATIONAL IDENTITY CARD", IsMandatory = true, DisplayOrder = 4, MaxFileSizeKB = 700, IsActive = true, CreatedAt = DateTime.UtcNow, CreatedBy = superAdmin.Id },
                    new DocumentType { Id = Guid.NewGuid(), Code = "BANK_ACCOUNT_DETAILS", Name = "BANK ACCOUNT DETAILS OF THE APPLICANT WITH THE MORTGAGE LENDER", IsMandatory = true, DisplayOrder = 5, MaxFileSizeKB = 700, IsActive = true, CreatedAt = DateTime.UtcNow, CreatedBy = superAdmin.Id },
                    new DocumentType { Id = Guid.NewGuid(), Code = "MORTGAGE_LOAN_OFFER_LETTER", Name = "MORTGAGE LOAN OFFER LETTER FROM THE MORTGAGE LENDER AND ACCEPTANCE", IsMandatory = true, DisplayOrder = 6, MaxFileSizeKB = 700, IsActive = true, CreatedAt = DateTime.UtcNow, CreatedBy = superAdmin.Id },
                    new DocumentType { Id = Guid.NewGuid(), Code = "PROPERTY_OFFER_LETTER", Name = "PROPERTY OFFER LETTER FROM THE OWNER SHOWING THE ADDRESS OF THE BUILDING AND ACCEPTANCE", IsMandatory = true, DisplayOrder = 7, MaxFileSizeKB = 700, IsActive = true, CreatedAt = DateTime.UtcNow, CreatedBy = superAdmin.Id },
                    new DocumentType { Id = Guid.NewGuid(), Code = "VERIFICATION_OF_PROPERTY_LETTER", Name = "VERIFICATION OF PROPERTY LETTER FROM THE MORTGAGE LENDER", IsMandatory = true, DisplayOrder = 8, MaxFileSizeKB = 700, IsActive = true, CreatedAt = DateTime.UtcNow, CreatedBy = superAdmin.Id },
                    new DocumentType { Id = Guid.NewGuid(), Code = "CONSENT_LETTER_RSA_HOLDER", Name = "CONSENT LETTER SIGNED BY RSA HOLDER", IsMandatory = true, DisplayOrder = 9, MaxFileSizeKB = 700, IsActive = true, CreatedAt = DateTime.UtcNow, CreatedBy = superAdmin.Id },
                    new DocumentType { Id = Guid.NewGuid(), Code = "INDEMNITY_FORM_APPLICANTS", Name = "COMPLETED INDEMNITY FORM FROM APPLICANTS(S) SWORN AT THE HIGH COURT", IsMandatory = true, DisplayOrder = 10, MaxFileSizeKB = 700, IsActive = true, CreatedAt = DateTime.UtcNow, CreatedBy = superAdmin.Id },
                    new DocumentType { Id = Guid.NewGuid(), Code = "INDEMNITY_FORM_MORTGAGE_LENDER", Name = "COMPLETED INDEMNITY FORM FROM MORTGAGE LENDER", IsMandatory = true, DisplayOrder = 11, MaxFileSizeKB = 700, IsActive = true, CreatedAt = DateTime.UtcNow, CreatedBy = superAdmin.Id },
                    new DocumentType { Id = Guid.NewGuid(), Code = "PENSION_COMPLIANCE_CERTIFICATE", Name = "COPY OF CURRENT PENSION COMPLIANCE CERTIFICATE OF MORTGAGE LENDER", IsMandatory = true, DisplayOrder = 12, MaxFileSizeKB = 700, IsActive = true, CreatedAt = DateTime.UtcNow, CreatedBy = superAdmin.Id },
                    new DocumentType { Id = Guid.NewGuid(), Code = "RSA_STATEMENT_AND_TRANSFER_HISTORY", Name = "ENDORSED RETIREMENT SAVINGS ACCOUNT STATEMENT FROM ACCESS PENSIONS (AND TRANSFER HISTORY IF ANY)", IsMandatory = true, DisplayOrder = 13, MaxFileSizeKB = 700, IsActive = true, CreatedAt = DateTime.UtcNow, CreatedBy = superAdmin.Id },
                    new DocumentType { Id = Guid.NewGuid(), Code = "PROPERTY_VALUATION_REPORT", Name = "VALUATION REPORT ON THE PROPERTY FROM LICENSED ESTATE SURVEYOR AND VALUERS (WITH CERTIFIED TRUE COPY STAMP)", IsMandatory = true, DisplayOrder = 14, MaxFileSizeKB = 700, IsActive = true, CreatedAt = DateTime.UtcNow, CreatedBy = superAdmin.Id },
                    new DocumentType { Id = Guid.NewGuid(), Code = "PROPERTY_INSURANCE", Name = "PROPERTY INSURANCE WITH RIGHT AMOUNT INSURED (WITH CERTIFIED TRUE COPY STAMP)", IsMandatory = true, DisplayOrder = 15, MaxFileSizeKB = 700, IsActive = true, CreatedAt = DateTime.UtcNow, CreatedBy = superAdmin.Id },
                    new DocumentType { Id = Guid.NewGuid(), Code = "CREDIT_LIFE_INSURANCE", Name = "CREDIT LIFE INSURANCE/ MORTGAGE PROTECTION INSURANCE WITH THE RIGHT AMOUNT (WITH CERTIFIED TRUE COPY STAMP)", IsMandatory = true, DisplayOrder = 16, MaxFileSizeKB = 700, IsActive = true, CreatedAt = DateTime.UtcNow, CreatedBy = superAdmin.Id },
                    new DocumentType { Id = Guid.NewGuid(), Code = "CONFIRMATION_OF_AVAILABILITY", Name = "CONFIRMATION OF AVAILABILTY OF PROPERTY (WITH CERTIFIED TRUE COPY STAMP)", IsMandatory = true, DisplayOrder = 17, MaxFileSizeKB = 700, IsActive = true, CreatedAt = DateTime.UtcNow, CreatedBy = superAdmin.Id },
                    new DocumentType { Id = Guid.NewGuid(), Code = "PROPERTY_TITLE_DOCUMENT", Name = "PROPERTY TITLE DOCUMENT AND LEGAL SEARCH (WITH CERTIFIED TRUE COPY STAMP)", IsMandatory = true, DisplayOrder = 18, MaxFileSizeKB = 700, IsActive = true, CreatedAt = DateTime.UtcNow, CreatedBy = superAdmin.Id },
                    new DocumentType { Id = Guid.NewGuid(), Code = "CONFIRMATION_OF_TITLE", Name = "CONFIRMATION OF TITLE DOCUMENT (WITH CERTIFIED TRUE COPY STAMP)", IsMandatory = true, DisplayOrder = 19, MaxFileSizeKB = 700, IsActive = true, CreatedAt = DateTime.UtcNow, CreatedBy = superAdmin.Id },
                    new DocumentType { Id = Guid.NewGuid(), Code = "READINESS_TO_DISBURSE", Name = "READINESS TO DISBURSE (WITH CERTIFIED TRUE COPY STAMP)", IsMandatory = true, DisplayOrder = 20, MaxFileSizeKB = 700, IsActive = true, CreatedAt = DateTime.UtcNow, CreatedBy = superAdmin.Id },
                    new DocumentType { Id = Guid.NewGuid(), Code = "APPLICATION_FORM_TO_MORTGAGE_LENDER", Name = "COPY OF APPLICATION FORM TO MORTGAGE LENDER", IsMandatory = true, DisplayOrder = 21, MaxFileSizeKB = 700, IsActive = true, CreatedAt = DateTime.UtcNow, CreatedBy = superAdmin.Id },

                    // Optional Documents (3)
                    new DocumentType { Id = Guid.NewGuid(), Code = "MARRIAGE_CERTIFICATE", Name = "MARRIAGE CERTIFICATE (FOR JOINT APPLICATION)", IsMandatory = false, DisplayOrder = 22, MaxFileSizeKB = 700, IsActive = true, CreatedAt = DateTime.UtcNow, CreatedBy = superAdmin.Id },
                    new DocumentType { Id = Guid.NewGuid(), Code = "CONSENT_LETTER_LOSS_JOB", Name = "CONSENT LETTER IF THE CLIENT HAS ACCESSED 25% FOR LOSS JOB", IsMandatory = false, DisplayOrder = 23, MaxFileSizeKB = 700, IsActive = true, CreatedAt = DateTime.UtcNow, CreatedBy = superAdmin.Id },
                    new DocumentType { Id = Guid.NewGuid(), Code = "PASSPORT_PHOTOGRAPH", Name = "RECENT PASSPORT PHOTOGRAPH OF EACH APPLICANT(S) AND SPOUSE(S) FOR JOINT MORTGAGE", IsMandatory = false, DisplayOrder = 24, MaxFileSizeKB = 700, IsActive = true, CreatedAt = DateTime.UtcNow, CreatedBy = superAdmin.Id },
                };

                context.DocumentTypes.AddRange(defaultDocTypes);
                await context.SaveChangesAsync();

                Console.WriteLine($"[DbSeeder] Inserted 24 new DocumentTypes successfully");
            }
            else
            {
                Console.WriteLine($"[DbSeeder] DocumentTypes are correct, no reseed needed");
            }

            // Seed default system configurations
            if (!await context.SystemConfigurations.AnyAsync())
            {
                var configs = new List<SystemConfiguration>
                {
                    new SystemConfiguration { Id = Guid.NewGuid(), ConfigKey = "MaxAssignmentsPerReviewer", ConfigValue = "20", Description = "Maximum active assignments per reviewer", LastModifiedBy = superAdmin.Id, LastModifiedAt = DateTime.UtcNow },
                    new SystemConfiguration { Id = Guid.NewGuid(), ConfigKey = "WorkloadWarningThreshold", ConfigValue = "15", Description = "Threshold to warn reviewers about workload", LastModifiedBy = superAdmin.Id, LastModifiedAt = DateTime.UtcNow }
                };

                context.SystemConfigurations.AddRange(configs);
                await context.SaveChangesAsync();
            }

            // Seed default role login access control settings
            if (!await context.RoleLoginAccessControls.AnyAsync())
            {
                var roleAccessSettings = new List<RoleLoginAccessControl>
                {
                    new RoleLoginAccessControl
                    {
                        Id = Guid.NewGuid(),
                        Role = "CLIENT",
                        IsLoginEnabled = false,
                        DisabledMessage = "Access is temporarily unavailable. Please try again during business hours or contact support.",
                        ChangeReason = "Initial system configuration - access temporarily disabled",
                        LastModifiedBy = superAdmin.Id,
                        LastModifiedAt = DateTime.UtcNow,
                        CreatedAt = DateTime.UtcNow
                    },
                    new RoleLoginAccessControl
                    {
                        Id = Guid.NewGuid(),
                        Role = "SERVICE_MANAGER",
                        IsLoginEnabled = false,
                        DisabledMessage = "Access is temporarily unavailable. Please try again during business hours or contact support.",
                        ChangeReason = "Initial system configuration - access temporarily disabled",
                        LastModifiedBy = superAdmin.Id,
                        LastModifiedAt = DateTime.UtcNow,
                        CreatedAt = DateTime.UtcNow
                    }
                };

                context.RoleLoginAccessControls.AddRange(roleAccessSettings);
                await context.SaveChangesAsync();
                Console.WriteLine("[DbSeeder] Role login access control settings seeded successfully");
            }
        }
    }
}
