# How to Update DocumentType Creation with SuperAdmin CreatedBy

## ✅ COMPLETED: Updated Scripts

### 1. **Phase2_CreateDocumentType_IdentityDocument.sql** ✓
**Status: COMPLETED WITH CREATEDBY LOOKUP**

This script now includes:
- **SuperAdmin User Lookup** with multiple fallback strategies
- **CreatedBy Field** populated properly
- **IsMandatory = 0** (optional document type)
- **Error Handling** if no admin user exists
- **Comprehensive Output** showing all details

**Key Features:**
```sql
-- Find SUPER_ADMIN user (with fallbacks)
DECLARE @SuperAdminId UNIQUEIDENTIFIER = (
	SELECT TOP 1 Id FROM MMGTDB2A.dbo.Users 
	WHERE LOWER(Role) = 'super_admin' 
	AND LOWER(Email) = 'superadmin@pfa.com'
	AND IsActive = 1
)

-- If not found, try any SUPER_ADMIN
-- If not found, try any ADMIN

-- Insert with CreatedBy
INSERT INTO MMGTDB2A.dbo.DocumentTypes (
	...,
	CreatedAt,
	CreatedBy
)
VALUES (
	...,
	GETDATE(),
	@SuperAdminId  -- ← Now populated!
)
```

---

## Phase2_COMPLETE_ALL_IN_ONE.sql - Needs Update

The all-in-one script needs the same SuperAdmin lookup. Here's the section that needs updating:

### Current Code (Lines 42-56):
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

### Updated Code (Replace with this):
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

---

## Understanding the Changes

### What Changed?

| Aspect | Before | After |
|--------|--------|-------|
| **CreatedAt** | Missing (violated NOT NULL) ⚠️ | GETDATE() (current time) ✓ |
| **CreatedBy** | Missing (violated FK constraint) ⚠️ | SuperAdmin GUID (valid FK) ✓ |
| **IsMandatory** | Optional but needs validation | 0 (explicitly optional) ✓ |
| **Error Handling** | Silent failure | Clear error if no admin ✓ |
| **User Lookup** | None | Multiple fallback strategies ✓ |

### Why This Matters

**Without CreatedBy:**
```sql
-- This violates table constraints:
INSERT INTO DocumentTypes (..., IsMandatory, ...) 
VALUES (..., 0, ...)
-- ERROR: Column 'CreatedBy' does not allow NULLs
-- ERROR: Cannot insert NULL into CreatedAt if no DEFAULT provided
```

**With CreatedBy:**
```sql
-- Now complies with all constraints:
INSERT INTO DocumentTypes (Id, Code, Name, ..., CreatedAt, CreatedBy)
VALUES (@NewId, 'identity_document', ..., GETDATE(), @SuperAdminId)
-- ✓ SUCCESS: All required columns populated
```

---

## The Super Admin Lookup Strategy

### Priority Order:

**Level 1 (Exact Match):**
```sql
WHERE LOWER(Role) = 'super_admin' 
  AND LOWER(Email) = 'superadmin@pfa.com'
  AND IsActive = 1
```
Finds the seeded default super admin

**Level 2 (Any Super Admin):**
```sql
WHERE LOWER(Role) = 'super_admin'
  AND IsActive = 1
```
Takes the most recently created super admin

**Level 3 (Any Admin):**
```sql
WHERE LOWER(Role) = 'admin'
  AND IsActive = 1
```
Falls back to regular admin if no super admin exists

**Level 4 (Error):**
```sql
RAISERROR('Cannot create DocumentType: No SUPER_ADMIN or ADMIN user found...', 16, 1)
```
Clear error message explaining what to do

---

## DocumentType Table Schema (Reminder)

```sql
CREATE TABLE DocumentTypes (
	Id uniqueidentifier NOT NULL,                    -- GUID PK
	Code nvarchar(100) NOT NULL,                     -- 'identity_document'
	Name nvarchar(255) NOT NULL,                     -- 'identity_document'
	Description nvarchar(1000) NULL,                 -- Legacy on-disk docs
	IsMandatory bit NOT NULL,                        -- 0 (optional)
	DisplayOrder int NOT NULL,                       -- Next available
	MaxFileSizeKB int NOT NULL,                      -- 300
	IsActive bit NOT NULL,                           -- 1 (enabled)
	CreatedAt datetime2 NOT NULL DEFAULT GETDATE(),  -- ← Added
	CreatedBy uniqueidentifier NOT NULL,             -- ← Added (FK to Users)
	ModifiedAt datetime2 NULL,
	ModifiedBy uniqueidentifier NULL,

	CONSTRAINT PK_DocumentTypes PRIMARY KEY (Id),
	CONSTRAINT FK_DocumentTypes_Users_CreatedBy 
		FOREIGN KEY (CreatedBy) REFERENCES Users(Id) ON DELETE NO ACTION,
	CONSTRAINT FK_DocumentTypes_Users_ModifiedBy 
		FOREIGN KEY (ModifiedBy) REFERENCES Users(Id) ON DELETE NO ACTION
)
```

---

## How to Execute

### Option 1: Use the Standalone Script (RECOMMENDED)
```powershell
# Run this first to create identity_document
Invoke-Sqlcmd -ServerInstance "172.16.50.42" `
  -Database "MMGTDB2A" `
  -InputFile "Analysis\Phase2_CreateDocumentType_IdentityDocument.sql"
```

### Option 2: Use Phase 2 Complete All-In-One (After Manual Edit)
1. Edit `Phase2_COMPLETE_ALL_IN_ONE.sql` lines 42-56
2. Replace with the updated code above
3. Run the entire script

### Option 3: Manual SQL Execution in SSMS
1. Open SSMS
2. Connect to `172.16.50.42` / `MMGTDB2A`
3. Copy Phase2_CreateDocumentType_IdentityDocument.sql content
4. Execute

---

## Verification Query

After successfully creating identity_document, run:

```sql
-- Verify identity_document exists with proper CreatedBy
SELECT 
	Id,
	Code,
	Name,
	IsMandatory,
	MaxFileSizeKB,
	IsActive,
	CreatedAt,
	CreatedBy,
	u.Email as CreatedByEmail,
	u.Role as CreatedByRole
FROM MMGTDB2A.dbo.DocumentTypes dt
LEFT JOIN MMGTDB2A.dbo.Users u ON u.Id = dt.CreatedBy
WHERE LOWER(dt.Code) = 'identity_document'

-- Expected Result:
-- Id: [GUID]
-- Code: identity_document
-- Name: identity_document
-- IsMandatory: 0
-- MaxFileSizeKB: 300
-- IsActive: 1
-- CreatedAt: [Current DateTime]
-- CreatedBy: [SuperAdmin GUID]
-- CreatedByEmail: superadmin@pfa.com
-- CreatedByRole: SUPER_ADMIN
```

---

## What Gets Populated

### In DocumentTypes Table:
```
✓ Id = New GUID
✓ Code = 'identity_document'
✓ Name = 'identity_document'
✓ Description = 'Legacy on-disk document type for migrated records'
✓ IsMandatory = 0 (OPTIONAL)
✓ DisplayOrder = Next available (auto-calculated)
✓ MaxFileSizeKB = 300
✓ IsActive = 1 (enabled)
✓ CreatedAt = GETDATE() (now)
✓ CreatedBy = SuperAdmin GUID (required!)
✓ ModifiedAt = NULL
✓ ModifiedBy = NULL
```

### In ApplicationDocuments During Migration:
```
✓ DocumentTypeId = identity_document GUID (from mapping or fallback)
✓ LegacyDocumentType = Original source string
✓ IsLegacy = 1
✓ FileContent = NULL
✓ FilePath = Preserved from source
```

---

## Next Steps

1. **Option A (Recommended): Run Standalone Script**
   - Use `Phase2_CreateDocumentType_IdentityDocument.sql`
   - It's already updated with SuperAdmin lookup
   - Creates identity_document with proper CreatedBy

2. **Option B: Update All-In-One Script**
   - Edit `Phase2_COMPLETE_ALL_IN_ONE.sql`
   - Replace lines 42-56 with updated code above
   - Then run the complete migration

3. **Verify Creation**
   - Run the verification query above
   - Confirm CreatedBy is populated with SuperAdmin GUID
   - Confirm IsMandatory = 0

4. **Proceed with Phase 2 Migration**
   - Once identity_document exists, migration can proceed
   - DocumentTypeId will be populated with identity_document GUID
   - All legacy documents will have valid FK references

---

## Summary

✅ **Phase2_CreateDocumentType_IdentityDocument.sql**: Updated and ready to use
⚠️ **Phase2_COMPLETE_ALL_IN_ONE.sql**: Needs manual update (code provided above)
✅ **IsMandatory**: Set to 0 (optional document)
✅ **CreatedBy**: Auto-populated from SuperAdmin user
✅ **All FK constraints**: Will be satisfied
✅ **Error handling**: Clear messages if admin user missing

You're now ready to create identity_document with proper metadata! 🚀
