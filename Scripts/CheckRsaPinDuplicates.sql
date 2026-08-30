-- Script to check for RsaPin duplicates before applying migrations
-- Run this to see what duplicates exist in the database

-- Check duplicates in active status applications
SELECT 
	'Active Status Duplicates' as CheckType,
	RsaPin,
	COUNT(*) as DuplicateCount,
	COUNT(*) - 1 as WillBeDeleted,
	STRING_AGG(CAST(Id AS VARCHAR(MAX)), ', ') as DuplicateIds,
	MIN(CreatedAt) as OldestDate,
	MAX(CreatedAt) as NewestDate
FROM Applications
WHERE Status IN ('draft', 'submitted', 'under_review', 'returned', 'resubmitted')
	AND RsaPin IS NOT NULL
	AND RsaPin != ''
GROUP BY RsaPin
HAVING COUNT(*) > 1
ORDER BY DuplicateCount DESC;

-- Check duplicates in terminal status applications  
SELECT 
	'Terminal Status Duplicates' as CheckType,
	RsaPin,
	COUNT(*) as DuplicateCount,
	COUNT(*) - 1 as WillBeDeleted,
	STRING_AGG(CAST(Id AS VARCHAR(MAX)), ', ') as DuplicateIds,
	MIN(CreatedAt) as OldestDate,
	MAX(CreatedAt) as NewestDate
FROM Applications
WHERE Status IN ('approved', 'rejected', 'abandoned', 'cancelled')
	AND RsaPin IS NOT NULL
	AND RsaPin != ''
GROUP BY RsaPin
HAVING COUNT(*) > 1
ORDER BY DuplicateCount DESC;

-- Summary: Total records that will be deleted
SELECT 
	'SUMMARY' as ReportType,
	COUNT(*) as TotalApplicationsWithDuplicateRsaPins,
	(
		SELECT COUNT(*) - COUNT(DISTINCT RsaPin)
		FROM Applications
		WHERE RsaPin IS NOT NULL AND RsaPin != ''
	) as RecordsToBeDeleted
FROM Applications
WHERE RsaPin IN (
	SELECT RsaPin
	FROM Applications
	WHERE RsaPin IS NOT NULL AND RsaPin != ''
	GROUP BY RsaPin
	HAVING COUNT(*) > 1
);
