# PHASE 1 ANALYSIS - QUICK REFERENCE CHECKLIST

## ⚡ Quick Facts

| Aspect | Value | Notes |
|---|---|---|
| Source DB | MMGTDBCOPY | Production-like live data |
| Target DB | MMGTDB2A | Development schema (newer) |
| Total Records to Migrate | 59,105 | Users(110) + PMI(24) + Apps(13,789) + Docs(14,047) + History(31,135) |
| Identity Columns | NONE | All PKs are GUIDs |
| SET IDENTITY_INSERT Needed | NO | Not applicable for GUIDs |
| New Tables in Target | 10 | Will remain empty (ApplicationAssignments, EscalationRules, etc.) |
| New Columns in Applications | 12 | All nullable, mostly set to NULL |
| New Columns in ApplicationDocuments | 10 | Critical: IsLegacy=1, FileContent=NULL |
| FK Constraints | 5 | No circular dependencies ✅ |
| Unique Constraints | 6 | ApplicationNumber, CompanyCode, UserName (each with variations) |
| Migration Order | 5 phases | Users → PMICompanies → Applications → ApplicationDocuments → ApplicationReviewHistory |
| Risk Level | MEDIUM | High data volume; well-planned mitigation |
| Estimated Execution Time | 30-60 min | Dependent on server performance |

---

## 📋 Pre-Migration Checklist (Phase 2 Start)

### Prerequisites
- [ ] MMGTDBCOPY verified accessible
- [ ] MMGTDB2A verified accessible
- [ ] Backup of MMGTDBCOPY created
- [ ] Backup of MMGTDB2A created
- [ ] No active users connected to MMGTDB2A
- [ ] Maintenance window scheduled
- [ ] Team notified and on standby

### Data Quality Checks (Source Database)
- [ ] All Users have valid Ids (110 total)
- [ ] All PMICompanies have valid Ids (24 total)
- [ ] All Applications linked to valid PMICompanies
- [ ] All Applications linked to valid Users (or NULL)
- [ ] All ApplicationDocuments linked to valid Applications
- [ ] All ApplicationReviewHistory linked to valid Applications
- [ ] All ApplicationReviewHistory linked to valid Users
- [ ] No duplicate ApplicationNumbers in source
- [ ] No duplicate CompanyCode in source
- [ ] No duplicate UserName in source
- [ ] DocumentType values identified and mapped to target

### Target Database Prep
- [ ] Schema matches Phase 1 analysis (16 tables)
- [ ] DocumentTypes seeded (24 records expected)
- [ ] SystemConfigurations seeded (2 records expected)
- [ ] Target Users table contains only dev data (to be cleared)
- [ ] Target PMICompanies table contains only dev data (to be cleared)
- [ ] FK constraints exist on all core tables
- [ ] Unique constraints exist
- [ ] Column defaults verified

---

## 🔄 Migration Execution Order

### Phase 2: Core Lookups (No FK dependencies)
```
1. Users → 110 records
   - Direct copy, no transformations
   - Current target: 5 dev users (to be cleared)

2. PMICompanies → 24 records
   - Direct copy, no transformations
   - Current target: 1 dev company (to be cleared)
```

### Phase 3: Main Tables (Depend on Phase 2)
```
3. Applications → 13,789 records
   - Add values for new 12 columns (mostly NULL)
   - Map ApprovedAt → ApprovalDate
   - Check new columns nullable

4. ApplicationDocuments → 14,047 records
   - Add values for new 10 columns
   - CRITICAL: Set IsLegacy=1 for all
   - Set FileContent=NULL (legacy uses FilePath)
   - Set Version=1, IsLatestVersion=1
   - Map DocumentType string to DocumentTypeId (or NULL)
```

### Phase 4: History Tables (Depend on Phase 3)
```
5. ApplicationReviewHistory → 31,135 records
   - Direct copy, no transformations
   - Current target: 0 records
```

---

## 🔑 Critical Transformation Rules

### IsLegacy Flag (Most Critical)
```
ALL migrated ApplicationDocuments: IsLegacy = 1
├─ Indicates disk-based storage (FilePath in use)
├─ Prevents FileService.Replace/Delete operations
├─ FileService.Download reads FilePath fallback
├─ Protects legacy files from accidental modification
└─ New uploads after migration: IsLegacy = 0
```

### Applications New Columns
```
ApprovalDate ← ApprovedAt (map existing value)
All others ← NULL
├─ ApplicantId=NULL
├─ LoanAmount=NULL
├─ PropertyAddress=NULL
├─ PropertyValue=NULL
├─ LoanTerm=NULL
├─ InterestRate=NULL
├─ MortgageType=NULL
├─ ClosingDate=NULL
├─ Notes=NULL
├─ AssignedToUserId=NULL
└─ Priority=NULL
```

### ApplicationDocuments New Columns
```
Version ← 1 (default)
IsLatestVersion ← 1 (all migrated docs are only version)
IsLegacy ← 1 (CRITICAL: disk-based files)
FileContent ← NULL (BLOB only for new uploads)
DocumentTypeId ← NULL or mapped from DocumentType
All others ← NULL
├─ ReviewStatus=NULL
├─ ReviewComments=NULL
├─ ReviewedBy=NULL
├─ ReviewedAt=NULL
└─ UploadedBy=NULL
```

---

## ⚠️ Critical Decisions

| Decision | Value | Reason |
|---|---|---|
| Preserve GUID values | YES | Maintains all FK relationships |
| Disable constraints during migration | YES | Speeds up bulk INSERT; validates post-migration |
| Set IsLegacy=1 | YES | Protects legacy disk files from modification |
| FileContent=NULL | YES | Don't read physical files; legacy uses FilePath |
| Clear target first | YES | Remove dev seeded data; prevent unique key conflicts |
| Map DocumentType → DocumentTypeId | REQUIRED | Pre-migration task; nullable if unmapped |

---

## 🚨 Risks & Mitigations

| Risk | Severity | Mitigation | Tactic |
|---|---|---|---|
| FK violation on re-enable | HIGH | Verify all parents copied before children | Follow migration order |
| Missing legacy files after migration | HIGH | Do NOT move physical files; verify FilePath intact | Spot-check sample FilePath values |
| Data loss in new columns | MEDIUM | All new columns nullable or have defaults | Verify INSERT defaults apply |
| Duplicate unique values | MEDIUM | Clear target before migration; check source | DELETE from target tables first |
| ApplicationReviewHistory orphans | MEDIUM | Verify ApplicationId and ReviewerId valid | Validate source data quality |
| DocumentType mapping failures | MEDIUM | Create mapping; use NULL for unmapped | Pre-migration DocumentType query |
| Physical disk access issues | MEDIUM | Verify wwwroot/uploads path accessible | Test file system access |

---

## ✅ Post-Migration Validation

### Record Counts (Must Match Source)
```
Users: 110 ✓
PMICompanies: 24 ✓
Applications: 13,789 ✓
ApplicationDocuments: 14,047 ✓
ApplicationReviewHistory: 31,135 ✓
TOTAL: 59,105 ✓
```

### Data Integrity
```
✓ No orphaned Applications (all linked to valid PMICompanies)
✓ No orphaned Applications (all linked to valid Users or NULL)
✓ No orphaned ApplicationDocuments (all linked to valid Applications)
✓ No orphaned ApplicationReviewHistory (all linked to Applications)
✓ No orphaned ApplicationReviewHistory (all linked to Users)
✓ All FK constraints re-enabled successfully
✓ No unique constraint violations
```

### ApplicationDocuments Transformation Verification
```
✓ All records have IsLegacy=1
✓ All records have FileContent=NULL
✓ All records have Version=1
✓ All records have IsLatestVersion=1
✓ FilePath values preserved (sample check)
✓ DocumentTypeId either NULL or valid FK
```

### Application Functionality
```
✓ GET /api/applications (lists all 13,789)
✓ GET /api/applications/{id} (loads single application)
✓ GET /api/applications/{id}/documents (lists application documents)
✓ GET /api/documents/{id}/download (downloads legacy document)
✓ POST /api/documents (new document upload creates IsLegacy=0, FileContent populated)
✓ /api/reviewer-workload (shows applications for review)
```

---

## 📊 Success Metrics

| Metric | Target | Verification Query |
|---|---|---|
| Users migrated | 110 | `SELECT COUNT(*) FROM MMGTDB2A.dbo.Users;` → Should be 110 |
| PMICompanies migrated | 24 | `SELECT COUNT(*) FROM MMGTDB2A.dbo.PMICompanies;` → Should be 24 |
| Applications migrated | 13,789 | `SELECT COUNT(*) FROM MMGTDB2A.dbo.Applications;` → Should be 13,789 |
| ApplicationDocuments migrated | 14,047 | `SELECT COUNT(*) FROM MMGTDB2A.dbo.ApplicationDocuments;` → Should be 14,047 |
| ApplicationReviewHistory migrated | 31,135 | `SELECT COUNT(*) FROM MMGTDB2A.dbo.ApplicationReviewHistory;` → Should be 31,135 |
| IsLegacy flag set | 14,047 (100%) | `SELECT COUNT(*) FROM MMGTDB2A.dbo.ApplicationDocuments WHERE IsLegacy=1;` → Should be 14,047 |
| FileContent NULL | 14,047 (100%) | `SELECT COUNT(*) FROM MMGTDB2A.dbo.ApplicationDocuments WHERE FileContent IS NULL;` → Should be 14,047 |
| Constraints re-enabled | SUCCESS | Try to update FK column; should succeed only if new value valid |
| No FK orphans | ZERO | Run orphan detection queries; should return 0 rows |

---

## 🛑 Stop Conditions (Rollback Triggers)

**If ANY of these occur → IMMEDIATELY ROLLBACK:**

1. FK constraint re-enable fails → Data integrity issue detected
2. Unique constraint violation on any table → Duplicate values in source or target
3. Record count mismatch > 1% → Data loss detected
4. ApplicationDocuments.IsLegacy not all=1 → Transformation failed
5. ApplicationDocuments.FileContent not all=NULL → Transformation failed
6. Orphaned ApplicationDocuments or ApplicationReviewHistory detected → FK violation
7. Application API returns errors → Data corruption suspected
8. Legacy file download fails → FilePath corruption
9. New document upload fails → PostMigration schema issue

**Rollback procedure:**
1. Restore MMGTDB2A from backup
2. Investigate error
3. Verify Phase 1 analysis after fix
4. Reattempt Phase 2

---

## 📝 Documentation Review Checklist

- [ ] Read Phase1_EXECUTIVE_SUMMARY.md
- [ ] Understand column mapping (Phase1_ColumnMapping_Complete.md)
- [ ] Understand FK strategy (Phase1_IdentityFK_Analysis.md)
- [ ] Understand transformations (Phase1_DataTransformation_Strategy.md)
- [ ] Verify all risks understood and mitigated
- [ ] Confirm team understands rollback procedure
- [ ] Confirm backup process verified
- [ ] Identify Phase 2 assignee(s)

---

## 🟢 Ready for Phase 2

**✅ All Phase 1 deliverables completed:**
- ✅ Complete source-to-target table mapping
- ✅ Identity column strategy
- ✅ Relationship migration order
- ✅ Data transformation requirements
- ✅ Risks identified and documented
- ✅ Validation checklist prepared
- ✅ Rollback strategy outlined

**⏭️ Next: Phase 2 Implementation**
- Generate complete migration SQL scripts
- Execute pre-migration validation
- Execute data migration
- Execute post-migration validation
- Perform user acceptance testing

---

**PHASE 1 STATUS: ✅ COMPLETE & APPROVED FOR PHASE 2**

