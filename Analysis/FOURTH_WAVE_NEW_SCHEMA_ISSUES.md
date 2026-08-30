# Phase 2 Migration: Fourth Wave of Schema Issues - Analysis

## 🚨 NEW ERRORS (Just Discovered)

### Error 4.1: Applications.FirstName
```
ERROR Applications: Cannot insert the value NULL into column 'FirstName', 
table 'MMGTDB2A.dbo.Applications'; column does not allow nulls. INSERT fails.
```

**Analysis:**
- Applications INSERT on line 225-232 doesn't include FirstName
- FirstName NOT NULL in target Applications table
- NOT in source MMGTDBCOPY.dbo.Applications
- Likely has a default or should be omitted from schema

**Status:** BLOCKING - Applications migration failed

---

### Error 4.2: ApplicationDocuments.Version
```
ERROR ApplicationDocuments: Cannot insert the value NULL into column 'Version', 
table 'MMGTDB2A.dbo.ApplicationDocuments'; column does not allow nulls. INSERT fails.
```

**Analysis:**
- ApplicationDocuments INSERT on line 305-320 doesn't include Version
- Version NOT NULL in target ApplicationDocuments table
- NOT in source MMGTDBCOPY.dbo.ApplicationDocuments
- Should default to a version number (likely 1 for initial creation)

**Status:** BLOCKING - ApplicationDocuments migration failed

---

## ✓ SUCCESSES (Wave 3 Fixes Worked!)

### Success: Applications.Priority
- ✓ NO error about NULL Priority
- ✓ Fix #1 from Wave 3 worked perfectly

### Success: ApplicationReviewHistory.Status
- ✓ 31,135 rows migrated successfully
- ✓ NO error about NULL Status
- ✓ Fix #2 from Wave 3 worked perfectly
- ✓ Significant volume of legacy review history preserved

---

## 🔍 INVESTIGATION NEEDED

### Question 1: Applications.FirstName
**Where does FirstName belong?**
- Is it a mistake in the target schema?
- Should it reference a user's first name?
- Is there a JOIN needed to get this value?
- Can it default to empty string or 'N/A'?

**Steps to Determine:**
```sql
-- Check Applications.FirstName column definition
USE MMGTDB2A
SELECT COLUMN_NAME, DATA_TYPE, IS_NULLABLE, COLUMN_DEFAULT
FROM INFORMATION_SCHEMA.COLUMNS
WHERE TABLE_NAME = 'Applications' AND COLUMN_NAME = 'FirstName'

-- Check if FirstName makes sense in Applications context
-- (Usually Applications don't have names, Users do)

-- Check source schema - does MMGTDBCOPY have FirstName?
USE MMGTDBCOPY
SELECT COLUMN_NAME
FROM INFORMATION_SCHEMA.COLUMNS
WHERE TABLE_NAME = 'Applications'
ORDER BY ORDINAL_POSITION
```

### Question 2: ApplicationDocuments.Version
**What should Version be?**
- Is it a document version number?
- Should it be 1 for initial uploads?
- Should it increment on replacements?
- Can it default to 0 or 1?

**Steps to Determine:**
```sql
-- Check ApplicationDocuments.Version column definition
USE MMGTDB2A
SELECT COLUMN_NAME, DATA_TYPE, IS_NULLABLE, COLUMN_DEFAULT
FROM INFORMATION_SCHEMA.COLUMNS
WHERE TABLE_NAME = 'ApplicationDocuments' AND COLUMN_NAME = 'Version'

-- Check if Version has a default
-- Check existing data for pattern

-- Check source - does it have Version?
USE MMGTDBCOPY
SELECT COLUMN_NAME
FROM INFORMATION_SCHEMA.COLUMNS
WHERE TABLE_NAME = 'ApplicationDocuments'
ORDER BY ORDINAL_POSITION
```

---

## 📊 CURRENT MIGRATION STATUS

| Table | Status | Notes |
|---|---|---|
| identity_document | ✓ Complete | Already exists from Wave 2 |
| PMICompanies | ✓ Complete | 24 rows migrated |
| Users | ✓ Complete | 0 rows (seeded users preserved) |
| **Applications** | ❌ FAILED | NULL FirstName error |
| ApplicationReviewHistory | ✓ Complete | 31,135 rows migrated! |
| **ApplicationDocuments** | ❌ FAILED | NULL Version error |

---

## 📈 SURPRISING FINDS

### Finding 1: Large ApplicationReviewHistory Volume
- Successfully migrated: 31,135 rows
- This is MUCH larger than expected
- Suggests review history is critical to this system
- Confirms Wave 3 Status='Completed' fix was valuable

### Finding 2: Metadata Columns in Target
- ApplicationDocuments.Version - suggests versioning capability
- Applications.FirstName - unusual for an applications table
- These may be recent schema additions not reflected in source

---

## 🛠️ POSSIBLE SOLUTIONS

### For Applications.FirstName

**Option A: Default to empty string**
```sql
INSERT INTO MMGTDB2A.dbo.Applications (
	..., FirstName, ...
)
SELECT
	..., '' as FirstName, ...
```

**Option B: Default to 'N/A'**
```sql
INSERT INTO MMGTDB2A.dbo.Applications (
	..., FirstName, ...
)
SELECT
	..., 'N/A' as FirstName, ...
```

**Option C: Reference UserId to get FirstName**
```sql
INSERT INTO MMGTDB2A.dbo.Applications (
	..., FirstName, ...
)
SELECT
	..., ISNULL(u.FirstName, 'N/A') as FirstName, ...
FROM MMGTDBCOPY.dbo.Applications a
LEFT JOIN MMGTDBCOPY.dbo.Users u ON a.UserId = u.Id
```

**Option D: Remove from target (if column shouldn't exist)**
- Would require schema recovery SQL
- Not ideal mid-migration

### For ApplicationDocuments.Version

**Option A: Default to 1 (initial version)**
```sql
INSERT INTO MMGTDB2A.dbo.ApplicationDocuments (
	..., Version, ...
)
SELECT
	..., 1 as Version, ...
```

**Option B: Default to 0**
```sql
INSERT INTO MMGTDB2A.dbo.ApplicationDocuments (
	..., Version, ...
)
SELECT
	..., 0 as Version, ...
```

**Option C: Use existing data if source has it**
- Requires source investigation

---

## 🎯 RECOMMENDED NEXT STEPS

### Immediate (Within 5 minutes):
1. Run discovery queries to understand FirstName and Version columns
2. Check if these columns have defaults defined
3. Check if source schema has these columns

### Short-term (Within 15 minutes):
1. Make decision on default values
2. Update migration script with new fixes
3. Re-run migration

### Decision Points for User:

**For Applications.FirstName:**
- [ ] Use empty string ''
- [ ] Use 'N/A'
- [ ] Reference from Users table (JOIN on UserId)
- [ ] Other approach?

**For ApplicationDocuments.Version:**
- [ ] Default to 1 (initial version)
- [ ] Default to 0
- [ ] Other value?

---

## 🔄 WHAT THIS MEANS

We've discovered that the target schema has evolved beyond what the source schema provides:
- Applications now expects FirstName (likely business requirement)
- ApplicationDocuments now tracks Version (likely audit trail)

This is common in progressive development where target evolves faster than data sync.

The good news:
- ✓ Our 3 prior waves of fixes still work!
- ✓ ApplicationReviewHistory migration was massive success
- ✓ Pattern for fixing these is established
- ✓ Process is working (expose schema mismatch → diagnose → fix → retry)

---

## 📝 FILE STATUS

**Needs Update:** Analysis/Phase2_COMPLETE_ALL_IN_ONE.sql
- Line 225-232: Add FirstName to Applications INSERT
- Line 305-320: Add Version to ApplicationDocuments INSERT

**Need Discovery First:** Schema for:
- Applications.FirstName (type, meaning, default)
- ApplicationDocuments.Version (type, range, meaning)

