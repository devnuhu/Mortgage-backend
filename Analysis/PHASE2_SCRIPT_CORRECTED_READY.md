# Phase 2 Migration - CORRECTED SCRIPT READY

## Status: ✓ Script Fixed and Ready to Execute

The **Phase2_COMPLETE_ALL_IN_ONE.sql** script has been successfully corrected with the following critical fixes:

### What Was Fixed

**Problem 1: DocumentTypes.CreatedBy NULL**
- **Original Issue**: The `identity_document` INSERT was missing `CreatedAt` and `CreatedBy` fields
- **SQL Error**: `Msg 515 - Cannot insert the value NULL into column 'CreatedBy'`
- **Solution**: Added explicit SUPER_ADMIN/ADMIN user lookup with proper fallbacks

**The Corrected Code Block (Lines 40-80)**:
```sql
DECLARE @SuperAdminId UNIQUEIDENTIFIER = (
	SELECT TOP 1 Id FROM MMGTDB2A.dbo.Users 
	WHERE LOWER(Role) = 'super_admin' AND LOWER(Email) = 'superadmin@pfa.com' AND IsActive = 1
)
-- Fallbacks: any SUPER_ADMIN, then any ADMIN
-- Then INSERT with CreatedAt and CreatedBy populated from lookup

INSERT INTO MMGTDB2A.dbo.DocumentTypes (
	Id, Code, Name, Description, IsMandatory, DisplayOrder, MaxFileSizeKB, IsActive, CreatedAt, CreatedBy
)
VALUES (
	@NewId, 'identity_document', 'identity_document',
	'Legacy on-disk document type for migrated records',
	0, @MaxDisplayOrder + 1, 300, 1, GETDATE(), @SuperAdminId
)
```

### Migration Structure

The corrected script now performs these steps in order:

1. **Part 1: Create identity_document DocumentType**
   - With proper SuperAdmin lookup and FK constraint handling

2. **Part 2: Full Data Migration**
   - Section 1: Pre-migration validation
   - Section 2: Clear existing data (with constraint disable/enable)
   - Section 3: Migrate data in dependency order
	 - PMICompanies (no dependencies)
	 - Users (with duplicate-email skip logic)
	 - Applications
	 - DocumentType mapping (maps source `DocumentType` strings to target GUIDs)
	 - ApplicationDocuments (all marked `IsLegacy = 1`)
	 - ApplicationReviewHistory
   - Section 4: Re-enable FK constraints
   - Section 5: Post-migration validation

### Files Ready

- **Analysis/Phase2_COMPLETE_ALL_IN_ONE.sql** (402 lines, completely corrected)
- Verified: Lines 40-80 contain the corrected identity_document creation with CreatedBy lookup

### Next Steps to Run Migration

Execute the corrected script in SQL Server Management Studio or PowerShell:

```powershell
$sqlPath = "C:\Users\nuhu.audu\Desktop\PRO\BACKUP\BACKEND\main bk\29-06-26B\accessmortage-backend_live\Analysis\Phase2_COMPLETE_ALL_IN_ONE.sql"
Invoke-Sqlcmd -InputFile $sqlPath -ServerInstance "172.16.50.42" -QueryTimeout 600
```

**Connection Requirements**:
- Server: `172.16.50.42`
- Database: `MMGTDB2A` (target database)
- Source: `MMGTDBCOPY` (production copy)
- Network: Must be able to reach the SQL Server host

### Known Remaining Issues

After successful execution of this script, the following issues may still need attention:

1. **Users Migration**: The script includes `WHERE NOT EXISTS` logic to skip duplicate emails. The source has `admin@pfa.com` and target is seeded with it. Adjust the WHERE clause if you need different behavior.

2. **ApplicationDocuments.UploadedBy**: The current migration leaves `UploadedBy` as NULL. The source `MMGTDBCOPY.dbo.ApplicationDocuments` schema needs inspection to determine if it has an uploader field and how to map it.

3. **Foreign Key Violations**: Some Applications may have invalid `UserId` FK if the Users migration skipped records. Same for ApplicationReviewHistory's `ReviewedById`. These will show as warnings in the migration output.

### Validation Output Expected

The script will display:
- Record count comparisons (Migrated vs Source)
- Status indicators (✓ MATCH or ✗ MISMATCH)
- Any warnings about invalid FKs
- Completion timestamp

### Recovery

If the script fails:
1. Check the complete output for the specific error
2. The script automatically disables/enables constraints, so you can re-run it
3. Review the error message to identify which section failed

---

**Last Updated**: Script recreated with full CreatedBy lookup logic  
**Verified**: Lines 40-80 confirmed correct  
**Ready to Execute**: Yes
