-- PHASE 2: Database Cleanup - Update FileType from 'Legacy' to 'application/pdf'
-- IMPORTANT: Run backup queries first to validate record counts
-- This script updates ONLY the FileType column for records currently set to 'Legacy'

-- STEP 1: Verify record count BEFORE update
PRINT 'STEP 1: BEFORE UPDATE - Counting records with FileType=''Legacy'''
SELECT COUNT(*) AS RecordsToUpdate
FROM ApplicationDocuments
WHERE FileType = 'Legacy';

-- STEP 2: Execute the update
PRINT 'STEP 2: EXECUTING UPDATE - Setting FileType=''application/pdf'' where FileType=''Legacy'''
UPDATE ApplicationDocuments
SET FileType = 'application/pdf',
	UpdatedAt = GETUTCDATE()
WHERE FileType = 'Legacy';

PRINT 'Update completed'

-- STEP 3: Verify update result
PRINT 'STEP 3: AFTER UPDATE - Verifying no records remain with FileType=''Legacy'''
SELECT COUNT(*) AS RemainingLegacyRecords
FROM ApplicationDocuments
WHERE FileType = 'Legacy';

-- STEP 4: Verify the update was applied
PRINT 'STEP 4: Confirming new FileType value count'
SELECT COUNT(*) AS RecordsNowWithApplicationPDF
FROM ApplicationDocuments
WHERE FileType = 'application/pdf';

-- STEP 5: Sample verification - show a few updated records
PRINT 'STEP 5: Sample of updated records'
SELECT TOP 10
	Id,
	ApplicationId,
	FileName,
	FileType,
	IsLegacy,
	FilePath,
	UpdatedAt
FROM ApplicationDocuments
WHERE FileType = 'application/pdf'
ORDER BY UpdatedAt DESC;

PRINT 'Database cleanup completed successfully'
