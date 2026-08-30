# Phase 2: Legacy Document Handling - Investigation Index

## Overview

This directory contains the complete investigation of legacy document handling before Phase 2 live data migration from MMGTDBCOPY to MMGTDB2A.

**Investigation Scope**: Trace the complete flow from frontend request → API endpoint → controller → service → database → file system.

**Status**: ✅ **Investigation Complete** (No code/database changes made)

---

## Quick Navigation

### Start Here

1. **[Phase2_Quick_Reference.md](Phase2_Quick_Reference.md)** ⭐ **READ FIRST**
   - One-page summary with key facts
   - Critical tables & columns
   - API endpoints
   - Pre-migration checklist
   - 5 minutes to read

2. **[Phase2_Legacy_Document_Handling_Investigation_Report.md](Phase2_Legacy_Document_Handling_Investigation_Report.md)**
   - Comprehensive investigation findings
   - Section-by-section breakdown
   - Code references with line numbers
   - All 12 DocumentTypes listed
   - 20-30 minutes to read

3. **[Phase2_Legacy_Document_Investigation.sql](Phase2_Legacy_Document_Investigation.sql)**
   - 12 SQL queries for data validation
   - Ready to execute against MMGTDB2A
   - Checks for: consistency, orphans, integrity
   - Pre-migration validation checklist

---

## Key Findings Summary

### 1. Legacy Document Identification

**Question**: How are legacy documents identified?  
**Answer**: By `IsLegacy = 1` in `ApplicationDocuments` table

### 2. Storage Mechanism

**Question**: Where are legacy documents stored?  
**Answer**: On physical disk at paths like: `uploads/{applicationId}/{documentId}.pdf`

### 3. Download Flow

**Question**: How do users download legacy documents?  
**Answer**: 
```
GET /api/file/download/{fileId}
  → FileService.DownloadFileAsync()
  → Returns FileContent if BLOB, else File.ReadAllBytesAsync(FilePath)
  → Streamed to browser as PDF
```

### 4. Backend Support

**Question**: Does backend support both BLOB and disk storage?  
**Answer**: ✅ Yes - `FileService.DownloadFileAsync()` handles both transparently

### 5. Coexistence

**Question**: Can a single application have both legacy and new documents?  
**Answer**: ✅ Yes - different IsLegacy values, same application

### 6. Protection Rules

**Question**: Can legacy documents be replaced/deleted?  
**Answer**: ❌ No - `FileService` blocks operations if `IsLegacy = 1`

### 7. DocumentTypes

**Question**: Is there a separate "Legacy Document" type?  
**Answer**: ❌ No - 24 types exist, any can be legacy or new

### 8. Migration Safety

**Question**: Will Phase 2 preserve legacy document access?  
**Answer**: ✅ Yes - if IsLegacy flag and FilePath are preserved during copy

---

## Investigation Checklist

### Phase 1: Legacy Document Retrieval Flow ✅

- [x] Frontend request flow identified
- [x] API endpoint traced: `GET /api/file/download/{fileId}`
- [x] Controller method: `FileController.DownloadFile()`
- [x] Service layer: `FileService.DownloadFileAsync()`
- [x] Database query: `ApplicationDocuments` lookup
- [x] File system: `File.ReadAllBytesAsync(FilePath)`
- [x] Download mechanism: ASP.NET Core File() result

### Phase 2: Database Document Storage Analysis ✅

- [x] Core table identified: `ApplicationDocuments`
- [x] Columns mapped: FilePath, FileContent, IsLegacy, DocumentTypeId
- [x] Related tables: `DocumentTypes`, `Applications`
- [x] Legacy vs. BLOB distinction confirmed
- [x] AllApplicationDocuments fields documented

### Phase 3: DocumentType Investigation ✅

- [x] All 24 document types listed (see report)
- [x] Categories: 21 mandatory, 3 optional
- [x] No legacy-specific category found
- [x] IsLegacy mechanism identified (flag-based, not type-based)
- [x] All types have MaxFileSizeKB = 300

### Phase 4: Legacy Document Data Validation ✅

- [x] SQL queries created (12 total)
- [x] Validation checks defined
- [x] Consistency verification available
- [x] Orphan detection available
- [x] Referential integrity checks available

### Phase 5: Blob Storage Compatibility ✅

- [x] BLOB storage implementation confirmed
- [x] New documents: FileContent populated, FilePath NULL
- [x] Legacy documents: FileContent NULL, FilePath populated
- [x] Both types downloadable via same endpoint
- [x] Coexistence verified

---

## Files in This Investigation

### Investigation Reports

| File | Purpose | Read Time |
|------|---------|-----------|
| `Phase2_Quick_Reference.md` | One-page summary | 5 min |
| `Phase2_Legacy_Document_Handling_Investigation_Report.md` | Detailed findings | 20-30 min |
| `Phase2_Legacy_Document_Investigation.md` | This index | 5 min |

### SQL Validation

| File | Purpose | Queries |
|------|---------|---------|
| `Phase2_Legacy_Document_Investigation.sql` | Data validation | 12 |

### Code Files Referenced (Not Modified)

| File | Purpose | Key Classes |
|------|---------|------------|
| `Services/FileService.cs` | File operations | `DownloadFileAsync()`, `DeleteFileAsync()` |
| `Controllers/FileController.cs` | API endpoints | `DownloadFile()`, `GetFilesByApplication()` |
| `Models/ApplicationDocument.cs` | Entity schema | `IsLegacy`, `FilePath`, `FileContent` |
| `Models/DocumentType.cs` | Type configuration | 24 seeded types |
| `Data/DbSeeder.cs` | Seed data | DocumentType seeding |

---

## Critical Tables Summary

### ApplicationDocuments

**Purpose**: Core document storage table  
**Records**: Varies (includes all uploads, both legacy and new)

**Critical Columns for Legacy**:
- `Id` (GUID): Primary key
- `IsLegacy` (bit): **1 = disk-based, 0 = BLOB**
- `FilePath` (nvarchar 500): **Disk location (legacy only)**
- `FileContent` (varbinary MAX): **BLOB bytes (new only)**
- `ApplicationId` (GUID): Parent application
- `DocumentTypeId` (GUID): Document category

### DocumentTypes

**Purpose**: Configurable document categories  
**Records**: 24 (fixed, seeded at startup)

**Examples**:
- `NATIONAL_IDENTITY_CARD` (mandatory, 300 KB)
- `PROOF_OF_INCOME` (mandatory, 300 KB)
- `ADDITIONAL_DOCUMENTS` (optional, 300 KB)

### Applications

**Purpose**: Parent application records  
**Relationship**: 1 App → Many ApplicationDocuments

---

## API Endpoints for Legacy Documents

### Download

```http
GET /api/file/download/{fileId}
Authorization: Bearer {token}
```

Response: PDF file stream

### List Documents

```http
GET /api/file/application/{applicationId}
Authorization: Bearer {token}
```

Response: JSON array with documents (includes IsLegacy flag)

---

## Pre-Migration Validation

### Step 1: Run SQL Queries

Execute queries from `Phase2_Legacy_Document_Investigation.sql`:
- Query 2: Count legacy documents
- Query 3: Verify legacy document paths
- Query 4-5: Check for inconsistencies
- Query 11-12: Validate foreign keys

### Step 2: Manual Verification

- [ ] Confirm disk files exist at stored FilePath locations
- [ ] Verify file sizes are ≤ 300 KB
- [ ] Check server can access file path after migration
- [ ] Backup physical files before migration

### Step 3: Data Copy Validation

- [ ] Copy data to MMGTDB2A
- [ ] Run FK queries against new database
- [ ] Test document download from new database
- [ ] Verify IsLegacy flag preserved

---

## Risks Identified

| Risk | Likelihood | Mitigation |
|------|-----------|-----------|
| FilePath invalid after server migration | Medium | Test file access from new server before go-live |
| Disk files deleted before copy | Medium | Backup all physical files |
| IsLegacy flag not copied correctly | Low | Validate with SQL queries |
| Referential integrity broken | Low | Run FK validation queries provided |
| Backend code deployed without support | Low | Code already supports both types |

---

## Next Steps

### Immediate (Investigation Complete)

- [x] Trace legacy document flow
- [x] Identify backend classes & methods
- [x] Map database tables & columns
- [x] List DocumentTypes & confirm no legacy-specific type
- [x] Verify data integrity checks available
- [x] Create validation SQL queries
- [x] Document coexistence capability

### Pending Approval

- [ ] Execute Phase 2 data migration
- [ ] Run pre-migration validation queries
- [ ] Backup physical files
- [ ] Copy data to MMGTDB2A
- [ ] Validate post-migration access
- [ ] Confirm legacy documents still downloadable

### Post-Migration

- [ ] Test legacy document download
- [ ] Verify new document uploads work (BLOB)
- [ ] Confirm users can access existing applications
- [ ] Monitor file access and storage

---

## How to Use These Documents

### If You Need...

**Quick overview** → Read `Phase2_Quick_Reference.md` (5 min)

**Detailed findings** → Read `Phase2_Legacy_Document_Handling_Investigation_Report.md` (20-30 min)

**To validate data** → Execute `Phase2_Legacy_Document_Investigation.sql` against MMGTDB2A

**To understand the code** → Reference line numbers in the detailed report, then open the files in VS Code

**To trace a specific flow** → Search for "[Frontend|API|Controller|Service|Database|File]" in the detailed report

---

## Documentation References

### Earlier Phases (From Previous Sessions)

| Phase | Document | Status |
|-------|----------|--------|
| Phase 1 | `Phase1_EXECUTIVE_SUMMARY.md` | Completed |
| Phase 1 | `Phase1_QUICK_REFERENCE.md` | Completed |
| Phase 1 | `Phase1_ColumnMapping_Complete.md` | Completed |
| Phase 1 | `Phase1_IdentityFK_Analysis.md` | Completed |
| Phase 1 | `Phase1_DataTransformation_Strategy.md` | Completed |
| Phase 1 | `Phase1_Quick_Analysis.sql` | Completed |

### File Handling Documentation (Earlier Sessions)

| Document | Purpose |
|----------|---------|
| `FILE_HANDLING_ARCHITECTURE.md` | Full hybrid storage model |
| `FILE_HANDLING_QUICK_REFERENCE.md` | Developer reference |
| `FILE_HANDLING_DOCUMENTATION_INDEX.md` | Navigation hub |

---

## Key Takeaways

### What Works ✅

1. **Legacy documents are fully supported** by the backend
2. **Both disk and BLOB documents coexist** in the same application
3. **Download logic handles both** transparently
4. **Read-only protection** prevents accidental modification
5. **Migration can preserve** legacy access if done correctly

### What to Watch ⚠️

1. **Physical file paths** must remain accessible after migration
2. **IsLegacy flag** must be preserved during data copy
3. **FilePath column** must not be NULL for legacy documents
4. **FileContent** should remain NULL for legacy documents
5. **Disk files** should be backed up before migration

### What Does NOT Exist ❌

1. **No separate legacy document type** - all types support both storage
2. **No migration script** yet - planned for Phase 2 execution
3. **No new code changes** needed - existing code supports both
4. **No database schema changes** needed - columns already exist

---

## Contact & Questions

**For investigation results**: See `Phase2_Legacy_Document_Handling_Investigation_Report.md`

**For data validation**: Execute `Phase2_Legacy_Document_Investigation.sql` and review results

**For next steps**: Await approval to execute Phase 2 migration

---

**Investigation Date**: 2025-01-15  
**Status**: ✅ Complete (Investigation-Only)  
**Approval Required**: For Phase 2 Execution  
**Token Usage**: Optimized with working memory snapshot
