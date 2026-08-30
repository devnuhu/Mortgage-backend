-- PHASE 2: Verification Queries - Validate database cleanup
-- Run these after the update to confirm success

-- Query 1: Verify NO records remain with FileType='Legacy'
PRINT 'VERIFICATION 1: Remaining FileType=''Legacy'' records (should be 0)'
SELECT COUNT(*) AS RemainingLegacyCount
FROM ApplicationDocuments
WHERE FileType = 'Legacy';

-- Query 2: Count records now with FileType='application/pdf'
PRINT 'VERIFICATION 2: Records updated to FileType=''application/pdf'''
SELECT COUNT(*) AS UpdatedToApplicationPDF
FROM ApplicationDocuments
WHERE FileType = 'application/pdf';

-- Query 3: Distribution of all FileType values
PRINT 'VERIFICATION 3: Complete FileType distribution'
SELECT 
	FileType,
	COUNT(*) AS RecordCount
FROM ApplicationDocuments
GROUP BY FileType
ORDER BY RecordCount DESC;

-- Query 4: Verify metadata integrity - all legacy records should have IsLegacy=1
PRINT 'VERIFICATION 4: IsLegacy flag integrity for application/pdf records (legacy origin)'
SELECT COUNT(*) AS LegacyOriginRecords
FROM ApplicationDocuments
WHERE FileType = 'application/pdf' AND IsLegacy = 1;

-- Query 5: Verify FilePath preservation
PRINT 'VERIFICATION 5: FilePath preservation (should match pre-update count)'
SELECT COUNT(*) AS RecordsWithFilePath
FROM ApplicationDocuments
WHERE FileType = 'application/pdf' AND IsLegacy = 1 AND FilePath IS NOT NULL;

-- Query 6: Verify no FileContent was added (should remain NULL)
PRINT 'VERIFICATION 6: FileContent remains NULL (not BLOB stored)'
SELECT COUNT(*) AS BlobStoredCount
FROM ApplicationDocuments
WHERE FileType = 'application/pdf' AND IsLegacy = 1 AND FileContent IS NOT NULL;

-- Query 7: Sample of updated records for visual inspection
PRINT 'VERIFICATION 7: Sample of 10 updated records'
SELECT TOP 10
	Id,
	ApplicationId,
	FileName,
	FilePath,
	FileType,
	IsLegacy,
	FileSize,
	UpdatedAt
FROM ApplicationDocuments
WHERE FileType = 'application/pdf' AND IsLegacy = 1
ORDER BY UpdatedAt DESC;

PRINT 'All verification queries completed'
