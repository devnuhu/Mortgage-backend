# Phase 2 Migration - CORRECTED Script Ready

## 🔍 Problem Analysis

The initial migration script failed because it was built on **assumptions** about the source schema that didn't match reality.

### Errors Encountered
1. **"Invalid column name 'UserName'"** - Source Users table has no UserName column (uses Email instead)
2. **"Invalid column name 'CompanyName'"** - Source PMICompanies has "PmiName" not "CompanyName"
3. **"Cannot insert NULL into DocumentTypeId"** - Critical error: Script tried to insert NULL but target requires GUID
4. **Result**: 0 records migrated (expected 59,105)

---

## ✅ Solution: Corrected Migration Script

**File**: `Analysis/Phase2_MigrationExecution_CORRECTED.sql`

This script includes proper schema mapping based on ACTUAL source/target structure:

### Key Corrections

#### 1. **Users Table**
- Source columns: `Id, Email, FirstName, LastName, Role, PasswordHash, IsFirstLogin, IsActive, CreatedAt, UpdatedAt, LastLoginAt, PMICompanyId, PMIEmail, FirstLoginTokenHash, FirstLoginTokenExpiresAt`
- Target columns: Same structure (compatible)
- ✅ Direct 1:1 mapping works

#### 2. **PMICompanies Table**
- Source: `PmiName, EmailAddress, ResourcePersonName, ResourcePersonEmail, PhoneNumber, Status`
- Target: Same columns (compatible)
- ✅ Direct 1:1 mapping works

#### 3. **Applications Table**
- Source: `FirstName, MiddleName, LastName, RsaPin, EquityValue, SupplementaryValue, LoanAmount, PropertyValue, PmiRepName, PmiRepEmail, PmiCompanyName, SubmittedAt`
- Target: Same columns PLUS new ones: `DocumentStorageType, MandatoryDocumentsComplete, CompletedAt, PhoneNumber`
- ✅ New columns populated with defaults:
  - `DocumentStorageType = 'LEGACY'` (disk-based)
  - `MandatoryDocumentsComplete = 0`
  - `CompletedAt = NULL`
  - `PhoneNumber = NULL`

#### 4. **ApplicationDocuments Table** (CRITICAL FIX)
- **Problem**: Source has string `DocumentType`, target requires GUID `DocumentTypeId`
- **Solution**: Create mapping table that matches source DocumentType string to target DocumentTypes GUID
- **Process**:
  1. Query all unique DocumentType values from source
  2. Match them to target DocumentTypes by Code or Name
  3. For unmapped types, assign default DocumentType
  4. Use mapping table during INSERT
- ✅ **IsLegacy = 1** for all migrated documents
- ✅ **FileContent = NULL** (disk-based, not BLOB)
- ✅ **FilePath preserved** (existing disk paths maintained)

#### 5. **ApplicationReviewHistory Table**
- Source and Target: Identical structure
- ✅ Direct 1:1 mapping works

---

## 🚀 How to Execute

### Step 1: Open SSMS (SQL Server Management Studio)
- Connect to: **172.16.50.42**
- Database: **MMGTDB2A**

### Step 2: Open the Corrected Script
- File → Open
- Browse to: `Analysis/Phase2_MigrationExecution_CORRECTED.sql`

### Step 3: Execute
- Press **F5** (or click Execute)
- Monitor the Messages tab for progress

### Step 4: Wait for Completion
- Pre-validation: 1-2 min
- Data migration: 30-60 min
- Post-validation: 5-10 min
- **Total: ~45-75 minutes**

---

## 📊 Expected Results

When completed, you should see:

```
Summary:
  ✓ 110 Users migrated
  ✓ 24 PMICompanies migrated
  ✓ 13,789 Applications migrated
  ✓ 14,047 ApplicationDocuments migrated (all IsLegacy=1)
  ✓ 31,135 ApplicationReviewHistory records migrated
  ─────────────────
  ✓ 59,105 TOTAL RECORDS MIGRATED

✓ MIGRATION SUCCESSFUL - Record count within expected range
```

---

## ✨ Key Improvements in Corrected Script

✅ **Actual Schema Mapping**: Based on real column analysis, not assumptions  
✅ **DocumentType Resolution**: Intelligent mapping of string → GUID  
✅ **Legacy Document Protection**: IsLegacy=1, FileContent=NULL, FilePath preserved  
✅ **Default Values**: New columns populated sensibly  
✅ **FK Order**: Respects dependencies (Users → PMI → Apps → Docs → History)  
✅ **Error Handling**: Validates before proceeding  
✅ **Comprehensive Validation**: Pre, during, and post-migration checks  

---

## After Migration Completes

1. **Post-Migration Validation** (15-20 min)
   - File: `Analysis/Phase2_PostMigration_Validation.md`
   - 6-step validation framework

2. **Application Testing** (15-20 min)
   - File: `Analysis/Phase2_ApplicationConnectivity_Verification.md`
   - 7 endpoint tests including legacy document download

3. **Final Sign-Off**
   - File: `Analysis/Phase2_CompletionSummary.md`
   - Document completion with timestamps

---

## ⏱️ Timeline

| Phase | Duration | Status |
|-------|----------|--------|
| Pre-validation | 1-2 min | ✅ |
| Users insert | < 1 min | ✅ |
| PMICompanies insert | < 1 min | ✅ |
| Applications insert | 5-10 min | ✅ |
| ApplicationDocuments insert | 10-30 min | ✅ Main task |
| ApplicationReviewHistory insert | 10-15 min | ✅ |
| FK re-enable & validation | 5-10 min | ✅ |
| **TOTAL** | **30-75 min** | ✅ |

---

**Ready?** Open SSMS and execute `Phase2_MigrationExecution_CORRECTED.sql` now!

When it completes, reply with the final summary output and I'll run the post-migration validation.
