# Execution Guide: Create identity_document with SuperAdmin CreatedBy

## 📝 What This Does

This guide shows exactly how to create the `identity_document` DocumentType with:
- ✅ `IsMandatory = 0` (optional document)
- ✅ `CreatedBy = SuperAdmin GUID` (auto-found)
- ✅ `CreatedAt = GETDATE()` (current time)
- ✅ All FK constraints satisfied

---

## 🎯 Three Ways to Execute

### METHOD 1: Execute Standalone SQL File (EASIEST)

**File Location:**
```
C:\Users\nuhu.audu\Desktop\PRO\BACKUP\BACKEND\main bk\29-06-26B\accessmortage-backend_live\
  └─ Analysis\Phase2_CreateDocumentType_IdentityDocument.sql
```

**Steps:**

1. **Open SSMS (SQL Server Management Studio)**

2. **Connect to Database:**
   - Server: `172.16.50.42`
   - Database: `MMGTDB2A`
   - Authentication: Windows/SQL Auth as configured

3. **Execute Script:**
   - File → Open → File
   - Select: `Phase2_CreateDocumentType_IdentityDocument.sql`
   - Click Execute (F5)

**Expected Output:**
```
=== ADDING LEGACY DOCUMENTTYPE: identity_document ===

⭕ identity_document DocumentType does not exist - creating...

  Searching for SUPER_ADMIN user...
  ✓ Found CreatedBy user: A1B2C3D4-E5F6-47G8-H9I0-...

✓ Created identity_document DocumentType
  Id: [GUID]
  Code: identity_document
  Name: identity_document
  IsMandatory: 0
  DisplayOrder: [N]
  MaxFileSizeKB: 300
  IsActive: 1
  CreatedBy: A1B2C3D4-E5F6-47G8-H9I0-...

=== VERIFICATION ===

✓ DocumentType ready for migration

Complete DocumentType details:

Id                                 Code                 Name                 Description                           IsMandatory DisplayOrder MaxFileSizeKB IsActive CreatedAt                 CreatedBy
A1B2C3D4-E5F6-47G8-H9I0-...       identity_document    identity_document    Legacy on-disk document type...      0           6            300           1        2026-01-15 14:23:45.123   A1B2C3D4-E5F6-47G8-H9I0-...

=== SUMMARY ===
TotalDocumentTypes: 7
identity_document_Count: 1
```

---

### METHOD 2: Execute via PowerShell

**Command:**
```powershell
cd "C:\Users\nuhu.audu\Desktop\PRO\BACKUP\BACKEND\main bk\29-06-26B\accessmortage-backend_live"

Invoke-Sqlcmd `
  -ServerInstance "172.16.50.42" `
  -Database "MMGTDB2A" `
  -InputFile "Analysis\Phase2_CreateDocumentType_IdentityDocument.sql" `
  -QueryTimeout 300
```

**Or shorter:**
```powershell
Invoke-Sqlcmd -S "172.16.50.42" -d "MMGTDB2A" `
  -i ".\Analysis\Phase2_CreateDocumentType_IdentityDocument.sql"
```

---

### METHOD 3: Execute All-In-One Script

**Before executing**, update lines 42-56 in `Phase2_COMPLETE_ALL_IN_ONE.sql`:

**Replace this (current):**
```sql
ELSE
BEGIN
	PRINT '⭕ Creating identity_document DocumentType...'

	DECLARE @MaxDisplayOrder INT = (SELECT ISNULL(MAX(DisplayOrder), 0) FROM MMGTDB2A.dbo.DocumentTypes)
	DECLARE @NewId UNIQUEIDENTIFIER = NEWID()

	INSERT INTO MMGTDB2A.dbo.DocumentTypes (
		Id, Code, Name, Description, IsMandatory, DisplayOrder, MaxFileSizeKB, IsActive
	)
	VALUES (
		@NewId, 'identity_document', 'identity_document',
		'Legacy on-disk document type for migrated records',
		0, @MaxDisplayOrder + 1, 300, 1
	)

	PRINT '✓ Created: ' + CAST(@NewId AS NVARCHAR(36))
END
```

**With this (updated):**
```sql
ELSE
BEGIN
	PRINT '⭕ Creating identity_document DocumentType...'

	-- Find SUPER_ADMIN user for CreatedBy (FK requirement)
	DECLARE @SuperAdminId UNIQUEIDENTIFIER = (
		SELECT TOP 1 Id FROM MMGTDB2A.dbo.Users 
		WHERE LOWER(Role) = 'super_admin' AND LOWER(Email) = 'superadmin@pfa.com' AND IsActive = 1
	)

	IF @SuperAdminId IS NULL
	BEGIN
		SET @SuperAdminId = (
			SELECT TOP 1 Id FROM MMGTDB2A.dbo.Users 
			WHERE LOWER(Role) = 'super_admin' AND IsActive = 1
			ORDER BY CreatedAt DESC
		)
	END

	IF @SuperAdminId IS NULL
	BEGIN
		SET @SuperAdminId = (
			SELECT TOP 1 Id FROM MMGTDB2A.dbo.Users 
			WHERE LOWER(Role) = 'admin' AND IsActive = 1
			ORDER BY CreatedAt DESC
		)
	END

	IF @SuperAdminId IS NULL
	BEGIN
		RAISERROR('Cannot create DocumentType: No SUPER_ADMIN or ADMIN user found. Run DbSeeder first.', 16, 1)
	END

	DECLARE @MaxDisplayOrder INT = (SELECT ISNULL(MAX(DisplayOrder), 0) FROM MMGTDB2A.dbo.DocumentTypes)
	DECLARE @NewId UNIQUEIDENTIFIER = NEWID()

	INSERT INTO MMGTDB2A.dbo.DocumentTypes (
		Id, Code, Name, Description, IsMandatory, DisplayOrder, MaxFileSizeKB, IsActive, CreatedAt, CreatedBy
	)
	VALUES (
		@NewId, 'identity_document', 'identity_document',
		'Legacy on-disk document type for migrated records',
		0, @MaxDisplayOrder + 1, 300, 1, GETDATE(), @SuperAdminId
	)

	PRINT '✓ Created: ' + CAST(@NewId AS NVARCHAR(36)) + ' (CreatedBy: ' + CAST(@SuperAdminId AS NVARCHAR(36)) + ')'
END
```

Then execute:
```powershell
Invoke-Sqlcmd -S "172.16.50.42" -d "MMGTDB2A" `
  -i ".\Analysis\Phase2_COMPLETE_ALL_IN_ONE.sql"
```

---

## ✅ Verification

After successful execution, run this query to verify:

```sql
USE MMGTDB2A
GO

-- Check identity_document exists with proper CreatedBy
SELECT 
	Id as DocumentTypeId,
	Code,
	Name,
	IsMandatory,
	MaxFileSizeKB,
	IsActive,
	CreatedAt,
	CreatedBy,
	u.Email as CreatedByEmail,
	u.Role as CreatedByRole
FROM dbo.DocumentTypes dt
LEFT JOIN dbo.Users u ON u.Id = dt.CreatedBy
WHERE LOWER(dt.Code) = 'identity_document'
GO

-- Expected Result:
-- DocumentTypeId: [GUID]
-- Code: identity_document
-- Name: identity_document
-- IsMandatory: 0 ← Check this is 0
-- MaxFileSizeKB: 300
-- IsActive: 1
-- CreatedAt: [Current DateTime]
-- CreatedBy: [SuperAdmin GUID] ← Should NOT be NULL
-- CreatedByEmail: superadmin@pfa.com (or other admin email)
-- CreatedByRole: SUPER_ADMIN (or ADMIN)
```

**Expected Output:**
```
DocumentTypeId                       Code               Name               IsMandatory MaxFileSizeKB IsActive CreatedAt                 CreatedBy                        CreatedByEmail       CreatedByRole
A1B2C3D4-E5F6-47G8-H9I0-J1K2L3M4... identity_document  identity_document  0           300          1        2026-01-15 14:23:45.123  A1B2C3D4-E5F6-47G8-H9I0-...   superadmin@pfa.com   SUPER_ADMIN
```

---

## 🔍 Troubleshooting

### Error: "Cannot create DocumentType: No SUPER_ADMIN or ADMIN user found"

**Cause:** The Users table is empty (DbSeeder hasn't run)

**Solution:**
```sql
-- Check if users exist:
SELECT TOP 10 Id, Email, Role, IsActive FROM dbo.Users
GO

-- If empty, you need to:
-- 1. Run DbSeeder from Program.cs
-- 2. Or manually insert an admin user:
INSERT INTO dbo.Users (
	Id, Email, FirstName, LastName, Role, PasswordHash, IsActive, CreatedAt, UpdatedAt
)
VALUES (
	NEWID(),
	'superadmin@pfa.com',
	'Super',
	'Admin',
	'SUPER_ADMIN',
	'BCrypt_HASH_HERE',  -- Get from DbSeeder or bcrypt online tool
	1,
	GETDATE(),
	GETDATE()
)
```

---

### Error: "Column 'CreatedBy' does not allow NULLs"

**Cause:** Old script without SuperAdmin lookup is being used

**Solution:** Download the updated `Phase2_CreateDocumentType_IdentityDocument.sql` from Analysis folder

---

### Error: "Foreign key constraint fails on CreatedBy"

**Cause:** SuperAdmin GUID is not a valid Users.Id

**Solution:** Re-run the script - it auto-validates the user before inserting

---

## 📊 Script Behavior

### If identity_document Already Exists:
```
✓ identity_document DocumentType already exists
  Id: A1B2C3D4-E5F6-47G8-H9I0-...
  Skipping insert

✓ DocumentType ready for migration
```

### If identity_document Needs to be Created:
```
⭕ identity_document DocumentType does not exist - creating...

  Searching for SUPER_ADMIN user...
  ✓ Found CreatedBy user: A1B2C3D4-E5F6-47G8-H9I0-...

✓ Created identity_document DocumentType
  Id: B2C3D4E5-F6G7-47H8-I9J0-K1L2M3N4O5P6
  Code: identity_document
  Name: identity_document
  IsMandatory: 0
  DisplayOrder: 6
  MaxFileSizeKB: 300
  IsActive: 1
  CreatedBy: A1B2C3D4-E5F6-47G8-H9I0-...

✓ DocumentType ready for migration
```

---

## 🎯 Next Steps After Creating identity_document

1. ✅ **Verify** - Run the verification query above
2. ✅ **Document** - Record the identity_document GUID
3. ✅ **Execute Full Migration** - Run Phase 2 migration script
4. ✅ **Validate FK** - Confirm ApplicationDocuments.DocumentTypeId references are valid

---

## 📋 Pre-Execution Checklist

Before executing, verify:

- [ ] Connected to correct server: `172.16.50.42`
- [ ] Connected to correct database: `MMGTDB2A`
- [ ] Script file exists: `Analysis\Phase2_CreateDocumentType_IdentityDocument.sql`
- [ ] At least one admin user exists in Users table
- [ ] No other processes are running schema modifications
- [ ] Backup of MMGTDB2A exists (already done per your notes)

---

## 📞 What Each Section of the Script Does

### Section 1: Check if identity_document Exists
```sql
DECLARE @ExistingId UNIQUEIDENTIFIER = (
	SELECT Id FROM MMGTDB2A.dbo.DocumentTypes 
	WHERE LOWER(Code) = 'identity_document' 
	AND LOWER(Name) = 'identity_document'
)
-- If found: SKIP INSERT
-- If not found: CREATE NEW
```

### Section 2: Find SuperAdmin User
```sql
-- Level 1: Exact match (superadmin@pfa.com with SUPER_ADMIN role)
-- Level 2: Any SUPER_ADMIN role
-- Level 3: Any ADMIN role
-- Level 4: Error if none found
```

### Section 3: Insert identity_document
```sql
INSERT INTO MMGTDB2A.dbo.DocumentTypes (
	Id,              -- New GUID
	Code,            -- 'identity_document'
	Name,            -- 'identity_document'
	Description,     -- 'Legacy on-disk document type...'
	IsMandatory,     -- 0 (OPTIONAL)
	DisplayOrder,    -- Next available
	MaxFileSizeKB,   -- 300
	IsActive,        -- 1 (enabled)
	CreatedAt,       -- GETDATE()
	CreatedBy        -- SuperAdmin GUID (auto-found)
)
```

### Section 4: Verify Success
```sql
DECLARE @DocTypeId = (SELECT Id... WHERE Code = 'identity_document')
IF @DocTypeId IS NOT NULL
	PRINT '✓ DocumentType ready for migration'
ELSE
	RAISERROR('Failed to create identity_document DocumentType', 16, 1)
```

---

## 🚀 Speed Execution Path

**Fastest way to execute (3 clicks):**

1. Open SSMS
2. Connect to `172.16.50.42` / `MMGTDB2A`
3. File → Open → `Analysis\Phase2_CreateDocumentType_IdentityDocument.sql`
4. F5 (Execute)
5. Done! ✓

**Estimated time:** 2-5 seconds

---

**Status:** ✅ Ready to execute!
