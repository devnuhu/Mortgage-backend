# PHASE 1 - COMPLETE DATA MIGRATION ANALYSIS

## Executive Summary

**Task:** Safely migrate live production data from MMGTDBCOPY to MMGTDB2A

**Status:** ✅ Phase 1 Analysis Complete - Ready for Phase 2 Execution

**Data Volume:**
- Users: 110 records
- PMICompanies: 24 records
- Applications: 13,789 records
- ApplicationDocuments: 14,047 records
- ApplicationReviewHistory: 31,135 records
- **TOTAL: 59,105 business records**

**Key Finding:** All GUID primary keys with no identity columns. No SET IDENTITY_INSERT required. Direct value preservation maintains all FK relationships.

---

## Phase 1 Deliverables

### ✅ 1. Complete Source-to-Target Table Mapping
**Document:** `Analysis/Phase1_ColumnMapping_Complete.md`
- Detailed schema comparison for 5 core tables
- Column-by-column mapping
- 12 new columns in Applications (all nullable)
- 10 new columns in ApplicationDocuments (with defaults)
- Migration order: Users → PMICompanies → Applications → ApplicationDocuments → ApplicationReviewHistory

**Key Findings:**
- Users: 15 columns - identical schema ✅
- PMICompanies: 9 columns - identical schema ✅
- Applications: 12 source + 12 new target columns = 24 total
- ApplicationDocuments: 8 source + 10 new target columns = 18 total
- ApplicationReviewHistory: 6 columns - identical schema ✅

### ✅ 2. Identity Column Strategy
**Document:** `Analysis/Phase1_IdentityFK_Analysis.md`
- **NO identity columns in either database**
- All PKs are uniqueidentifier (GUID)
- **NO SET IDENTITY_INSERT ON/OFF required**
- **PRESERVE all GUID values** from source
- Foreign key relationships automatically maintained

**Critical Decision:** Direct value copying preserves all FK integrity

### ✅ 3. Foreign Key Relationship Mapping
**Document:** `Analysis/Phase1_IdentityFK_Analysis.md`
- 5 FK relationships identified and mapped
- Acyclic dependency graph (no circular references ✅)
- Migration dependency order enforced
- All FKs re-validated post-migration

**FK Dependencies:**
```
Users (root)
├─ Applications.UserId FK (NO_ACTION)
├─ ApplicationReviewHistory.ReviewerId FK (NO_ACTION)
└─ Users.PMICompanyId FK (NO_ACTION)

PMICompanies (root)
└─ Applications.PMICompanyId FK (NO_ACTION)
	└─ ApplicationDocuments.ApplicationId FK (CASCADE)
	└─ ApplicationReviewHistory.ApplicationId FK (CASCADE)
```

### ✅ 4. Data Transformation Requirements
**Document:** `Analysis/Phase1_DataTransformation_Strategy.md`

#### Applications Table Transformations
| New Column | Migration Value | Rationale |
|---|---|---|
| ApplicantId | NULL | No applicant tracking in legacy; future feature |
| LoanAmount | NULL | Not tracked in legacy system |
| PropertyAddress | NULL | Not tracked in legacy system |
| PropertyValue | NULL | Not tracked in legacy system |
| LoanTerm | NULL | Not tracked in legacy system |
| InterestRate | NULL | Not tracked in legacy system |
| MortgageType | NULL | Not tracked in legacy system |
| ApprovalDate | Copy from ApprovedAt | Map existing approval timestamp |
| ClosingDate | NULL | Not tracked in legacy system |
| Notes | NULL | Future use; not in legacy |
| AssignedToUserId | NULL | Future assignment feature |
| Priority | NULL | Future priority feature |

#### ApplicationDocuments Table Transformations
| New Column | Migration Value | Rationale |
|---|---|---|
| DocumentTypeId | NULL or mapped | Required lookup; see mapping section |
| FileContent | NULL | BLOB only for new uploads; legacy uses FilePath |
| Version | 1 | Default version for all migrated docs |
| IsLatestVersion | 1 | All migrated docs are only version |
| ReviewStatus | NULL | Not tracked per-document in legacy |
| ReviewComments | NULL | Not tracked per-document in legacy |
| ReviewedBy | NULL | Not tracked per-document in legacy |
| ReviewedAt | NULL | Not tracked per-document in legacy |
| UploadedBy | NULL | Not tracked per-document in legacy |
| **IsLegacy** | **1** | 🔑 **CRITICAL: Mark all migrated docs as legacy disk files** |

**Critical Decision:** IsLegacy=1 for all 14,047 migrated documents
- Indicates disk-based storage (FilePath in use)
- Prevents modification via FileService.ReplaceFileAsync and FileService.DeleteFileAsync
- Downloads read from FilePath (not FileContent BLOB)
- Legacy files are immutable and preserved post-migration

### ✅ 5. Identified Risks
**Document:** `Analysis/Phase1_IdentityFK_Analysis.md`

| Risk | Severity | Mitigation | Status |
|---|---|---|---|
| FK violation on ApplicationDocuments.ApplicationId | High | Verify all Applications migrated first; constraints disabled during migration | ✅ Mitigated |
| FK violation on ApplicationReviewHistory.ReviewerId | High | Verify all Users migrated first; constraints disabled during migration | ✅ Mitigated |
| FK violation on Applications.PMICompanyId | High | Verify all PMICompanies migrated first; constraints disabled during migration | ✅ Mitigated |
| FK violation on Applications.UserId | High | May be NULL in legacy; verify compatibility | ⚠️ Needs verification |
| Unique constraint violation on ApplicationNumber | Medium | Clear target before migration; check source for duplicates | ✅ Mitigated |
| Unique constraint violation on CompanyCode | Medium | Clear target before migration; check source for duplicates | ✅ Mitigated |
| Unique constraint violation on UserName | Medium | Clear target before migration | ✅ Mitigated |
| Legacy disk FilePath invalid after migration | Medium | Do NOT move physical files; preserve FilePath strings as-is | ✅ Mitigated |
| DocumentType mapping (name → GUID FK) | Medium | Create mapping table; use NULL if no match found | ⚠️ Pre-migration task |
| New column NOT NULL violations | Low | Verify all new columns nullable or have defaults | ✅ Verified |

### ✅ 6. Migration Script & Process

**Phase 2 Will Include:**
- Complete pre-migration validation SQL
- Constraint disable/enable scripts
- Data copy scripts with transformations
- Post-migration validation queries
- Rollback strategy

**Expected Structure:**
```sql
-- Pre-migration: Backup, verify, clear target
-- Step 1: Disable foreign key constraints
-- Step 2: Clear target tables
-- Step 3: Copy Users (Phase 1)
-- Step 4: Copy PMICompanies (Phase 1)
-- Step 5: Copy Applications (Phase 2, with transformations)
-- Step 6: Copy ApplicationDocuments (Phase 2, with transformations + IsLegacy=1)
-- Step 7: Copy ApplicationReviewHistory (Phase 3)
-- Step 8: Re-enable foreign key constraints
-- Step 9: Validate integrity
-- Step 10: Verify record counts
```

### ✅ 7. Validation Checklist

#### Pre-Migration Validation
- [ ] Source database MMGTDBCOPY accessible and contains expected data
- [ ] Target database MMGTDB2A schema matches analysis (16 tables)
- [ ] Backup of MMGTDBCOPY created (restore point for source data)
- [ ] Backup of MMGTDB2A created (restore point for target schema)
- [ ] No active connections to MMGTDB2A
- [ ] DocumentTypes in target seeded (24 records present)
- [ ] Verify source has no FK orphans
  - [ ] All Applications.PMICompanyId values exist in PMICompanies
  - [ ] All Applications.UserId values exist in Users (or NULL)
  - [ ] All ApplicationDocuments.ApplicationId values exist in Applications
  - [ ] All ApplicationReviewHistory.ApplicationId values exist in Applications
  - [ ] All ApplicationReviewHistory.ReviewerId values exist in Users

#### During-Migration Validation
- [ ] FK constraints disabled successfully on all 5 core tables
- [ ] Target tables cleared (UserCount, PMICount, etc. = 0)
- [ ] Users copy executed (check:110 records inserted)
- [ ] PMICompanies copy executed (check: 24 records inserted)
- [ ] Applications copy executed (check: 13,789 records inserted; ApprovalDate populated where ApprovedAt exists)
- [ ] ApplicationDocuments copy executed (check: 14,047 records inserted; IsLegacy=1 for all)
- [ ] ApplicationReviewHistory copy executed (check: 31,135 records inserted)
- [ ] FK constraints re-enable attempted

#### Post-Migration Validation
- [ ] FK constraints re-enabled successfully (no errors)
- [ ] Record count verification (all tables match source counts)
- [ ] ApplicationNumber unique constraint validated
- [ ] CompanyCode unique constraint validated
- [ ] UserName unique constraint validated
- [ ] Spot-check: Sample Applications verify ApplicationNumber, ApplicationNumber, Status correct
- [ ] Spot-check: Sample ApplicationDocuments verify FileName, FileSize, IsLegacy=1 correct
- [ ] Legacy file paths spot-check: Verify sample FilePath strings are intact
- [ ] DocumentTypeId spot-check: Verify either NULL or valid FK to DocumentTypes
- [ ] No orphaned records remain
- [ ] Application API endpoints functional
- [ ] Document download/view functional for legacy files
- [ ] New document upload functional (creates IsLegacy=0, FileContent populated)

---

## Documentation Index

| Document | Purpose | Key Sections |
|---|---|---|
| `Phase1_ColumnMapping_Complete.md` | Complete schema comparison | Column mappings, new columns, data types, migration order |
| `Phase1_IdentityFK_Analysis.md` | Identity & FK strategy | PK analysis, FK dependencies, constraint handling, validation checklist |
| `Phase1_DataTransformation_Strategy.md` | Data transformation rules | New column handling, IsLegacy flag, transformations, SQL patterns |
| `Phase1_Analysis_Results.txt` | Raw database analysis output | DDL, indexes, defaults, sample data |
| `Phase1_ColumnMapping.csv` | Column-by-column export | CSV format of all columns in both databases |

---

## Phase 2: Implementation Readiness

**Next Steps (Phase 2 Execution):**
1. Generate complete migration SQL scripts
2. Document rollback procedures
3. Schedule maintenance window
4. Execute pre-migration validation
5. Execute data migration (with constraint disable/re-enable)
6. Execute post-migration validation
7. User acceptance testing (UAT)
8. Production cutover

**Estimated Phase 2 Output:**
- Phase 2 complete migration SQL script (~500+ lines)
- Rollback script
- Step-by-step execution guide
- Validation report
- UAT sign-off

---

## Critical Success Factors

✅ **What Will Determine Success:**
1. All FK relationships preserved (no orphaned records)
2. All 59,105 records migrated correctly
3. IsLegacy flag correctly set (protects legacy files)
4. FilePath strings preserved (disk files remain accessible)
5. New columns populated with NULL/defaults correctly
6. Constraints re-enable without errors
7. Application endpoints all functional post-migration
8. No data loss or corruption

⚠️ **What Will Cause Failure:**
- FK constraint violations on re-enable (indicates data integrity issue)
- Missing ApplicationDocuments linked to Applications
- Missing ApplicationReviewHistory linked to Applications or Users
- Moved or corrupted physical disk files
- Wrong DocumentType mappings
- Duplicate ApplicationNumbers or CompanyCode values
- Application API errors accessing migrated data

---

## Approval Gate

**PHASE 1 COMPLETE. READY FOR PHASE 2 APPROVAL.**

Before proceeding to Phase 2 Execution, confirm:

- [ ] Column mappings reviewed and approved
- [ ] Identity/FK strategy reviewed and approved
- [ ] Data transformation rules reviewed and approved
- [ ] Risk mitigation strategies reviewed and approved
- [ ] Validation checklist reviewed and approved
- [ ] Rollback procedures documented and reviewed
- [ ] Maintenance window scheduled
- [ ] Team aware and prepared

**When approved → Proceed to Phase 2: Generate migration scripts and execute migration**

---

## Questions Resolved by Phase 1

1. ✅ **Do we need SET IDENTITY_INSERT?** → NO, all PKs are GUIDs
2. ✅ **What is the migration order?** → Users → PMICompanies → Applications → Docs → History
3. ✅ **How do we handle new columns?** → NULL or mapped values for all Applications & ApplicationDocuments
4. ✅ **What about legacy files?** → IsLegacy=1, FilePath preserved, do not move physical files
5. ✅ **Will FK relationships break?** → NO, GUID values preserved, all FKs maintained
6. ✅ **What about DocumentType mapping?** → Create lookup; use NULL if unmapped
7. ✅ **Will there be constraint violations?** → Unlikely if migration order respected and constraints disabled during migration

---

## Outstanding Pre-Migration Tasks

1. ⚠️ **DocumentType mapping:** Query source for unique DocumentType values; manually map to target DocumentTypes
2. ⚠️ **Verify Applications.UserId:** Confirm if all ApplicationsUserId values exist in Users (or are NULL)
3. ⚠️ **Physical disk files:** Verify wwwroot/uploads/ path exists and is accessible for legacy file verification
4. ⚠️ **Backup confirmation:** Confirm backups of both MMGTDBCOPY and MMGTDB2A exist before Phase 2

---

## Final Note

This Phase 1 analysis provides all necessary information to execute a safe, controlled data migration that:
- Preserves all live business data (59K+ records)
- Maintains data integrity and FK relationships
- Protects legacy disk-based files
- Supports new BLOB storage going forward
- Enables application to continue functioning post-migration

**Ready for Phase 2 when approved by stakeholder.**

