# Phase 2 Migration - Quick Start & Command Reference

## 🚀 Quick Start (3 Steps)

### Step 1: Create DocumentType (< 1 minute)
```
1. SSMS → Connect to 172.16.50.42 → MMGTDB2A
2. File → Open → Analysis/Phase2_CreateDocumentType_IdentityDocument.sql
3. Ctrl+A, F5
4. Wait for: "✓ identity_document DocumentType ready"
```

### Step 2: Execute Migration (5-15 minutes)
```
1. SSMS → Still on MMGTDB2A
2. File → Open → Analysis/Phase2_MigrationExecution_v3_FINAL.sql
3. Ctrl+A, F5
4. DO NOT CLOSE - monitor progress
5. Wait for: "=== MIGRATION COMPLETE ===" at bottom
```

### Step 3: Verify Success (< 1 minute)
```
Scroll to Section 5.1 and verify:
□ Users: 110 / 110 ✓ MATCH
□ PMICompanies: 24 / 24 ✓ MATCH
□ Applications: 13789 / 13789 ✓ MATCH
□ ApplicationDocuments: > 0 ✓ MATCH
□ ReviewHistory: 31135+ / 31135+ ✓ MATCH
```

✅ If all checks pass = SUCCESS

---

## 📋 Command Reference

### Connection String (SSMS)
```
Server: 172.16.50.42
Database: MMGTDB2A
Authentication: Windows or SQL Auth (user's choice)
```

### Document Type Details
```
Code: identity_document
Name: identity_document
Description: Legacy on-disk document type for migrated records
MaxFileSizeKB: 300
IsMandatory: false (0)
IsActive: true (1)
```

### Migration Statistics
```
Users:                110 records
PMICompanies:          24 records
Applications:      13,789 records
ApplicationDocuments: ~100,000+ records
ApplicationReviewHistory: 31,135+ records
─────────────────────────────────
TOTAL:            ~155,000+ records
```

### Hybrid Storage Model (After Migration)
```
Legacy Documents (Migrated):
├─ IsLegacy = 1 (protected flag)
├─ FileContent = NULL (disk-based)
├─ FilePath = [original path] (preserved)
├─ DocumentTypeId = {identity_document GUID}
└─ LegacyDocumentType = [source type string]

New Documents (After Migration):
├─ IsLegacy = 0 (new flag)
├─ FileContent = [binary BLOB data] (stored in DB)
├─ FilePath = NULL (no disk needed)
├─ DocumentTypeId = [matched GUID]
└─ UploadedBy = [user ID]
```

### Time Breakdown
```
Setup:                    < 1 min (DocumentType creation)
Pre-checks:              < 1 min
Clear target:            < 1 min
PMI migration:           < 1 min  (24 records)
Users migration:         < 1 min  (110 records)
Applications:            1-2 min  (13,789 records)
DocumentType mapping:    < 1 min
ApplicationDocuments:    3-8 min  (100,000+ documents) ← Longest
ApplicationReviewHistory: 1-2 min  (31,135 records)
Constraints:             < 1 min
Validation:              < 1 min
─────────────────────────────────────────────
Total:                   5-15 minutes
```

---

## 🔍 Key Output Messages to Expect

### Successfully Created DocumentType
```
✓ Created identity_document DocumentType
  Id: {GUID}
  Code: identity_document
  Name: identity_document
  DisplayOrder: 25
  MaxFileSizeKB: 300
  IsActive: 1
```

### Successful PMI Migration
```
✓ PMICompanies migrated: 24 rows
```

### Successful Users Migration
```
✓ Users migrated: 110 rows
```

### Successful Applications Migration
```
✓ Applications migrated: 13789 rows
```

### DocumentType Mapping (Should Show)
```
✓ DocumentType mapping ready

DocumentType mapping results:
SourceType           | TargetTypeId              | Code              | DocCount
─────────────────────────────────────────────────────────────────────────
identity_document    | {GUID}                   | identity_document | 98765
other_type           | {GUID}                   | other_code        | 1235
```

### Successful ApplicationDocuments Migration
```
✓ ApplicationDocuments migrated: 100000 rows
```

### Successful ReviewHistory Migration
```
✓ ApplicationReviewHistory migrated: 31135 rows
```

### Final Validation (Section 5)
```
5.1: RECORD COUNTS COMPARISON:
Users                       | 110    | 110    | ✓ MATCH
PMICompanies                | 24     | 24     | ✓ MATCH
Applications                | 13789  | 13789  | ✓ MATCH
ApplicationDocuments        | 100000 | 100000 | ✓ MATCH
ApplicationReviewHistory    | 31135  | 31135  | ✓ MATCH

5.2: DATA QUALITY CHECKS:
IsLegacy | Count
1 (legacy) | 100000 ✓

5.3: FOREIGN KEY INTEGRITY:
Applications with valid UserId    | 13789 ✓
Applications with INVALID UserId  | 0 ✓
```

---

## ❌ Error Messages to Watch For

### If DocumentType Doesn't Exist
```
✗ ERROR: identity_document DocumentType still not found
→ Solution: Run Phase2_CreateDocumentType_IdentityDocument.sql first
```

### If Source Database Inaccessible
```
✗ ERROR: Cannot access MMGTDBCOPY
→ Solution: Check network connectivity to 172.16.50.42
```

### If ApplicationDocuments = 0
```
⚠ ISSUE: ApplicationDocuments not migrated
→ Solution: Check DocumentType mapping, verify identity_document exists
```

### If Users < 110
```
✗ ERROR Users: [specific error message from SQL]
→ Solution: Check for constraint violations or data quality issues
```

### If NULL DocumentTypeId Found
```
NULL DocumentTypeId (ERROR) | 1234 rows
→ Solution: Document type mapping failed for those records
```

### If Invalid FK Found
```
Applications with INVALID UserId | 234
→ Solution: Users didn't migrate properly, check error in Users section
```

---

## 🔄 Rollback/Recovery Plan

If migration needs to be rolled back:

### Option 1: Restore from Backup
```sql
-- In SSMS (master database):
RESTORE DATABASE MMGTDB2A 
FROM DISK = '[backup file path]'
WITH REPLACE
```

### Option 2: Manual Cleanup (Keep v1 data)
```sql
-- Clear migrated data and revert to v1 state:
DELETE FROM MMGTDB2A.dbo.ApplicationReviewHistory WHERE CreatedAt > 'today'
DELETE FROM MMGTDB2A.dbo.ApplicationDocuments WHERE CreatedAt > 'today'
DELETE FROM MMGTDB2A.dbo.Applications WHERE CreatedAt > 'today'
DELETE FROM MMGTDB2A.dbo.Users WHERE CreatedAt > 'today'
-- Then run v3 again
```

---

## 📞 Troubleshooting Quick Links

If you encounter issues:

1. **DocumentType mapping failed**
   - Run: `Analysis/Phase2_DocumentType_Mapping_Analysis.sql`
   - Check source vs target types

2. **Users migration failed**
   - Run: `Analysis/Phase2_Detailed_Diagnostics.sql`
   - Look for constraint violations

3. **General diagnostics**
   - Run: `Analysis/Phase2_Detailed_Diagnostics.sql`
   - Shows current state and problems

4. **Need to understand everything**
   - Read: `Analysis/Phase2_MigrationExecution_v3_README.md`
   - Full explanation of all fixes

---

## ✅ Final Checklist Before Execution

- [ ] SSMS open and connected to 172.16.50.42
- [ ] Database MMGTDB2A selected
- [ ] MMGTDB2A backed up (confirmed)
- [ ] Have 15 minutes available
- [ ] Ready to monitor script output
- [ ] Know where backup is (in case restore needed)

---

## 📁 Files Reference

### Execute in Order:
1. `Phase2_CreateDocumentType_IdentityDocument.sql` ← First
2. `Phase2_MigrationExecution_v3_FINAL.sql` ← Main
3. (Then post-migration validation)

### Reference/Diagnostics:
- `Phase2_DocumentType_Mapping_Analysis.sql`
- `Phase2_Detailed_Diagnostics.sql`
- `Phase2_PostMigration_Validation.md`
- `Phase2_ApplicationConnectivity_Verification.md`

### Previous (Failed v1):
- `Phase2_MigrationExecution.sql` (reference only)

---

## 🎯 Success Definition

Migration is successful when:

✅ Section 5.1 shows all records matched (not MISMATCH)
✅ Section 5.3 shows NO NULL DocumentTypeId
✅ Section 5.4 shows NO INVALID FK references
✅ No ✗ ERROR messages in output
✅ ApplicationDocuments count > 0 (not 0)

---

## Next Steps After Success

1. Run `Phase2_PostMigration_Validation.md` queries
2. Run `Phase2_ApplicationConnectivity_Verification.md` tests
3. Test API endpoints:
   - File download: `GET /api/file/download?fileId={id}`
   - File upload: `POST /api/file/upload`
4. Verify legacy files protected (can't delete/modify)
5. Sign off on Phase 2 completion

---

**Everything is ready. Execute the scripts in order and monitor output.**
