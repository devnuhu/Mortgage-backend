# Phase 2 Migration - CORRECTED and SYNTAX-VALIDATED

## ✓ Status: Script Ready & Verified

### What Was Fixed

1. **Removed All Special Characters** from PRINT statements that were causing SQL label conflicts
2. **Proper String Concatenation** for all messages
3. **Clean GO Batch Separators** between sections
4. **Verified SuperAdmin Lookup Logic** with proper fallbacks

### Critical Code Section (Lines 45-86)

The `identity_document` creation now includes:

```sql
DECLARE @SuperAdminId UNIQUEIDENTIFIER = (
	SELECT TOP 1 Id FROM MMGTDB2A.dbo.Users 
	WHERE LOWER(Role) = 'super_admin' 
	AND LOWER(Email) = 'superadmin@pfa.com' 
	AND IsActive = 1
)

-- Fallback 1: Any SUPER_ADMIN
IF @SuperAdminId IS NULL
BEGIN
	SET @SuperAdminId = (
		SELECT TOP 1 Id FROM MMGTDB2A.dbo.Users 
		WHERE LOWER(Role) = 'super_admin' AND IsActive = 1
		ORDER BY CreatedAt DESC
	)
END

-- Fallback 2: Any ADMIN
IF @SuperAdminId IS NULL
BEGIN
	SET @SuperAdminId = (
		SELECT TOP 1 Id FROM MMGTDB2A.dbo.Users 
		WHERE LOWER(Role) = 'admin' AND IsActive = 1
		ORDER BY CreatedAt DESC
	)
END

-- Error if still not found
IF @SuperAdminId IS NULL
BEGIN
	RAISERROR('Cannot create DocumentType: No SUPER_ADMIN or ADMIN user found.', 16, 1)
END

-- Insert with CreatedAt and CreatedBy
INSERT INTO MMGTDB2A.dbo.DocumentTypes (
	Id, Code, Name, Description, IsMandatory, DisplayOrder, MaxFileSizeKB, IsActive, CreatedAt, CreatedBy
)
VALUES (
	@NewId, 'identity_document', 'identity_document',
	'Legacy on-disk document type for migrated records',
	0, @MaxDisplayOrder + 1, 300, 1, GETDATE(), @SuperAdminId
)
```

### Migration Flow

**Part 1: Prepare DocumentType**
- Check if `identity_document` exists
- If not, create it with proper SuperAdmin lookup
- Verify creation succeeded

**Part 2: Execute Full Migration**

1. **Section 1: Pre-Migration Validation**
   - Verify source database accessible
   - Show source record counts

2. **Section 2: Prepare Target Tables**
   - Disable FK constraints
   - Clear existing data

3. **Section 3: Migrate Data** (in dependency order)
   - PMICompanies (0 dependencies)
   - Users (with duplicate-email skip)
   - Applications
   - DocumentType mapping (source string → target GUID)
   - ApplicationDocuments (all marked as Legacy)
   - ApplicationReviewHistory

4. **Section 4: Re-enable Constraints**
   - Re-enable all FK constraints

5. **Section 5: Post-Migration Validation**
   - Record count comparison
   - Data quality checks
   - DocumentType verification

### How to Run

**Option 1: SQL Server Management Studio**
1. Open `Analysis\Phase2_COMPLETE_ALL_IN_ONE.sql`
2. Connect to `172.16.50.42`
3. Click Execute (or Ctrl+F5)
4. Wait for completion message

**Option 2: PowerShell (with SSL workaround)**
```powershell
$sqlPath = "C:\Users\nuhu.audu\Desktop\PRO\BACKUP\BACKEND\main bk\29-06-26B\accessmortage-backend_live\Analysis\Phase2_COMPLETE_ALL_IN_ONE.sql"

# If you get SSL errors, use trusted connection:
Invoke-Sqlcmd -InputFile $sqlPath -ServerInstance "172.16.50.42" -TrustServerCertificate -QueryTimeout 600
```

**Option 3: sqlcmd with SSL bypass**
```cmd
sqlcmd -S "172.16.50.42" -U "sa" -P "P@ssw0rd1234" -i "Analysis\Phase2_COMPLETE_ALL_IN_ONE.sql" -C
```

### Expected Output

```
=============================================================================
PHASE 2: COMPLETE LIVE DATA MIGRATION
MMGTDBCOPY > MMGTDB2A (COMBINED SCRIPT)
=============================================================================

=============================================================================
PART 1: PREPARE DOCUMENTTYPE
=============================================================================

1.0: Checking for identity_document DocumentType...

[Either "CREATED: [GUID]" or "OK: identity_document DocumentType already exists"]

=== SECTION 1: PRE-MIGRATION VALIDATION ===
1.1: Verify MMGTDBCOPY accessible...
OK: MMGTDBCOPY accessible - ApplicationDocuments: [N] records

1.2: Source record counts:
[Record counts table]

=== SECTION 2: PREPARE TARGET TABLES ===
[Table cleanup messages]

=== SECTION 3: MIGRATE DATA ===
3.1: Migrating PMICompanies...
OK: PMICompanies migrated: [N] rows

3.2: Migrating Users...
OK: Users migrated: [N] rows

[... additional sections ...]

=== SECTION 5: POST-MIGRATION VALIDATION ===
5.1: RECORD COUNTS COMPARISON:
[Comparison table with MATCH/MISMATCH status]

=============================================================================
MIGRATION COMPLETE
Review validation results above
=============================================================================

Completion time: [timestamp]
```

### What to Check After Execution

1. **Record Counts** in Section 5.1
   - `PMICompanies`, `Applications`, `ApplicationReviewHistory` should show `MATCH`
   - `Users` will be less than source if duplicates were skipped
   - `ApplicationDocuments` count should match source

2. **Warnings**
   - Some Applications may have invalid UserId FK (expected if Users were skipped)
   - ReviewHistory may have invalid ReviewedById FK (same reason)

3. **Errors**
   - All errors are prefixed with `ERROR`
   - If any occur, the script continues and shows them in the output

### Known Issues & Solutions

**If identity_document fails to create:**
- Run DbSeeder first: `dotnet run --project Data/DbSeeder.csproj`
- Or manually insert a SUPER_ADMIN or ADMIN user

**If Users migration is empty:**
- The `admin@pfa.com` from DbSeeder causes duplicates
- Use `SELECT * FROM MMGTDB2A.dbo.Users` to see what was kept
- Adjust the WHERE NOT EXISTS logic if needed

**If ApplicationDocuments is incomplete:**
- Check `UploadedBy` column mapping (may not exist in source)
- Run diagnostic query to inspect source schema

### Files

- **Phase2_COMPLETE_ALL_IN_ONE.sql** (417 lines)
  - Full corrected migration script
  - Syntax validated (no label conflicts)
  - Ready to execute

---

**Last Updated**: Script recreated with correct syntax, special characters removed  
**Verification**: Lines 45-86 confirmed - SuperAdmin lookup + CreatedAt/CreatedBy insert  
**Status**: READY TO EXECUTE
