using Microsoft.EntityFrameworkCore;
using MMGT.API.Data;
using MMGT.API.Models;

namespace MMGT.API.Services
{
    /// <summary>
    /// Service for managing document types (SUPER_ADMIN only).
    /// Allows SUPER_ADMIN to add, edit, deactivate document types without code deployment.
    /// Maintains audit log of all changes.
    /// </summary>
    public interface IDocumentTypeService
    {
        Task<List<DocumentType>> GetAllDocumentTypesAsync(bool includeInactive = false);
        Task<DocumentType?> GetDocumentTypeByIdAsync(Guid id);
        Task<DocumentType?> GetDocumentTypeByCodeAsync(string code);
        Task<DocumentType> CreateDocumentTypeAsync(string code, string name, string? description, bool isMandatory, int displayOrder, int maxFileSizeKB, Guid createdBy);
        Task<DocumentType> UpdateDocumentTypeAsync(Guid id, string name, string? description, bool isMandatory, int displayOrder, int maxFileSizeKB, Guid modifiedBy);
        Task<bool> DeactivateDocumentTypeAsync(Guid id, Guid deactivatedBy);
        Task<bool> ReactivateDocumentTypeAsync(Guid id, Guid reactivatedBy);
        Task<List<DocumentTypeAuditLog>> GetAuditLogsAsync(Guid documentTypeId);
    }

    public class DocumentTypeService : IDocumentTypeService
    {
        private readonly ApplicationDbContext _context;

        public DocumentTypeService(ApplicationDbContext context)
        {
            _context = context;
        }

        /// <summary>
        /// Get all document types, optionally filtering out inactive ones.
        /// Returns sorted by DisplayOrder.
        /// </summary>
        public async Task<List<DocumentType>> GetAllDocumentTypesAsync(bool includeInactive = false)
        {
            var query = _context.DocumentTypes.AsQueryable();

            if (!includeInactive)
            {
                query = query.Where(dt => dt.IsActive);
            }

            return await query.OrderBy(dt => dt.DisplayOrder).ToListAsync();
        }

        /// <summary>
        /// Get document type by ID.
        /// </summary>
        public async Task<DocumentType?> GetDocumentTypeByIdAsync(Guid id)
        {
            return await _context.DocumentTypes.FindAsync(id);
        }

        /// <summary>
        /// Get document type by unique code (case-sensitive).
        /// </summary>
        public async Task<DocumentType?> GetDocumentTypeByCodeAsync(string code)
        {
            return await _context.DocumentTypes
                .FirstOrDefaultAsync(dt => dt.Code == code);
        }

        /// <summary>
        /// Create new document type.
        /// Code must be unique and is immutable after creation.
        /// Throws if code already exists.
        /// </summary>
        public async Task<DocumentType> CreateDocumentTypeAsync(
            string code, 
            string name, 
            string? description, 
            bool isMandatory, 
            int displayOrder, 
            int maxFileSizeKB, 
            Guid createdBy)
        {
            // Validate code uniqueness
            var existing = await GetDocumentTypeByCodeAsync(code);
            if (existing != null)
            {
                throw new InvalidOperationException($"Document type with code '{code}' already exists");
            }

            var documentType = new DocumentType
            {
                Id = Guid.NewGuid(),
                Code = code,
                Name = name,
                Description = description,
                IsMandatory = isMandatory,
                DisplayOrder = displayOrder,
                MaxFileSizeKB = maxFileSizeKB,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = createdBy
            };

            using (var transaction = await _context.Database.BeginTransactionAsync())
            {
                try
                {
                    _context.DocumentTypes.Add(documentType);
                    await _context.SaveChangesAsync();

                    // Log creation
                    var auditLog = new DocumentTypeAuditLog
                    {
                        Id = Guid.NewGuid(),
                        DocumentTypeId = documentType.Id,
                        FieldName = "Created",
                        OldValue = null,
                        NewValue = code,
                        ChangeType = "Created",
                        ChangeReason = $"New document type created: {name}",
                        ChangedBy = createdBy,
                        ChangedAt = DateTime.UtcNow
                    };

                    _context.DocumentTypeAuditLogs.Add(auditLog);
                    await _context.SaveChangesAsync();
                    await transaction.CommitAsync();
                }
                catch
                {
                    await transaction.RollbackAsync();
                    throw;
                }
            }

            return documentType;
        }

        /// <summary>
        /// Update document type properties (except Code, which is immutable).
        /// Creates audit log entries for each changed field.
        /// </summary>
        public async Task<DocumentType> UpdateDocumentTypeAsync(
            Guid id,
            string name,
            string? description,
            bool isMandatory,
            int displayOrder,
            int maxFileSizeKB,
            Guid modifiedBy)
        {
            var documentType = await GetDocumentTypeByIdAsync(id);
            if (documentType == null)
            {
                throw new InvalidOperationException($"Document type with ID '{id}' not found");
            }

            using (var transaction = await _context.Database.BeginTransactionAsync())
            {
                try
                {
                    // Track changes for audit log
                    var changes = new List<(string fieldName, string? oldValue, string newValue)>();

                    if (documentType.Name != name)
                    {
                        changes.Add(("Name", documentType.Name, name));
                        documentType.Name = name;
                    }

                    if (documentType.Description != description)
                    {
                        changes.Add(("Description", documentType.Description, description ?? string.Empty));
                        documentType.Description = description;
                    }

                    if (documentType.IsMandatory != isMandatory)
                    {
                        changes.Add(("IsMandatory", documentType.IsMandatory.ToString(), isMandatory.ToString()));
                        documentType.IsMandatory = isMandatory;
                    }

                    if (documentType.DisplayOrder != displayOrder)
                    {
                        changes.Add(("DisplayOrder", documentType.DisplayOrder.ToString(), displayOrder.ToString()));
                        documentType.DisplayOrder = displayOrder;
                    }

                    if (documentType.MaxFileSizeKB != maxFileSizeKB)
                    {
                        changes.Add(("MaxFileSizeKB", documentType.MaxFileSizeKB.ToString(), maxFileSizeKB.ToString()));
                        documentType.MaxFileSizeKB = maxFileSizeKB;
                    }

                    documentType.ModifiedAt = DateTime.UtcNow;
                    documentType.ModifiedBy = modifiedBy;

                    _context.DocumentTypes.Update(documentType);
                    await _context.SaveChangesAsync();

                    // Create audit log entries for each change
                    foreach (var (fieldName, oldValue, newValue) in changes)
                    {
                        var auditLog = new DocumentTypeAuditLog
                        {
                            Id = Guid.NewGuid(),
                            DocumentTypeId = id,
                            FieldName = fieldName,
                            OldValue = oldValue,
                            NewValue = newValue,
                            ChangeType = "Modified",
                            ChangeReason = null,
                            ChangedBy = modifiedBy,
                            ChangedAt = DateTime.UtcNow
                        };

                        _context.DocumentTypeAuditLogs.Add(auditLog);
                    }

                    await _context.SaveChangesAsync();
                    await transaction.CommitAsync();
                }
                catch
                {
                    await transaction.RollbackAsync();
                    throw;
                }
            }

            return documentType;
        }

        /// <summary>
        /// Deactivate a document type (soft delete).
        /// Existing applications with this document type remain unchanged.
        /// New applications cannot use deactivated document types.
        /// </summary>
        public async Task<bool> DeactivateDocumentTypeAsync(Guid id, Guid deactivatedBy)
        {
            var documentType = await GetDocumentTypeByIdAsync(id);
            if (documentType == null)
            {
                throw new InvalidOperationException($"Document type with ID '{id}' not found");
            }

            if (!documentType.IsActive)
            {
                return false; // Already inactive
            }

            using (var transaction = await _context.Database.BeginTransactionAsync())
            {
                try
                {
                    documentType.IsActive = false;
                    documentType.ModifiedAt = DateTime.UtcNow;
                    documentType.ModifiedBy = deactivatedBy;

                    _context.DocumentTypes.Update(documentType);
                    await _context.SaveChangesAsync();

                    // Log deactivation
                    var auditLog = new DocumentTypeAuditLog
                    {
                        Id = Guid.NewGuid(),
                        DocumentTypeId = id,
                        FieldName = "IsActive",
                        OldValue = "true",
                        NewValue = "false",
                        ChangeType = "Deactivated",
                        ChangeReason = "Document type deactivated",
                        ChangedBy = deactivatedBy,
                        ChangedAt = DateTime.UtcNow
                    };

                    _context.DocumentTypeAuditLogs.Add(auditLog);
                    await _context.SaveChangesAsync();
                    await transaction.CommitAsync();
                }
                catch
                {
                    await transaction.RollbackAsync();
                    throw;
                }
            }

            return true;
        }

        /// <summary>
        /// Reactivate a previously deactivated document type.
        /// </summary>
        public async Task<bool> ReactivateDocumentTypeAsync(Guid id, Guid reactivatedBy)
        {
            var documentType = await GetDocumentTypeByIdAsync(id);
            if (documentType == null)
            {
                throw new InvalidOperationException($"Document type with ID '{id}' not found");
            }

            if (documentType.IsActive)
            {
                return false; // Already active
            }

            using (var transaction = await _context.Database.BeginTransactionAsync())
            {
                try
                {
                    documentType.IsActive = true;
                    documentType.ModifiedAt = DateTime.UtcNow;
                    documentType.ModifiedBy = reactivatedBy;

                    _context.DocumentTypes.Update(documentType);
                    await _context.SaveChangesAsync();

                    // Log reactivation
                    var auditLog = new DocumentTypeAuditLog
                    {
                        Id = Guid.NewGuid(),
                        DocumentTypeId = id,
                        FieldName = "IsActive",
                        OldValue = "false",
                        NewValue = "true",
                        ChangeType = "Modified",
                        ChangeReason = "Document type reactivated",
                        ChangedBy = reactivatedBy,
                        ChangedAt = DateTime.UtcNow
                    };

                    _context.DocumentTypeAuditLogs.Add(auditLog);
                    await _context.SaveChangesAsync();
                    await transaction.CommitAsync();
                }
                catch
                {
                    await transaction.RollbackAsync();
                    throw;
                }
            }

            return true;
        }

        /// <summary>
        /// Get audit log entries for a specific document type.
        /// Sorted by ChangedAt descending (newest first).
        /// </summary>
        public async Task<List<DocumentTypeAuditLog>> GetAuditLogsAsync(Guid documentTypeId)
        {
            return await _context.DocumentTypeAuditLogs
                .Where(log => log.DocumentTypeId == documentTypeId)
                .OrderByDescending(log => log.ChangedAt)
                .ToListAsync();
        }
    }
}
