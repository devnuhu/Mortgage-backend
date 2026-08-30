@echo off
REM Quick check for RsaPin duplicates before migration
REM This script helps you see what data will be affected

echo Checking for RsaPin duplicates...
echo.
echo Run this T-SQL query against your database:
echo.
echo ============================================
echo WITH DuplicateCheck AS (
echo     SELECT 
echo         RsaPin,
echo         COUNT(*) as DuplicateCount,
echo         COUNT(*) - 1 as WillBeDeleted
echo     FROM Applications
echo     WHERE RsaPin IS NOT NULL AND RsaPin != ''
echo     GROUP BY RsaPin
echo     HAVING COUNT(*) ^> 1
echo )
echo SELECT SUM(WillBeDeleted) as TotalRecordsToDelete FROM DuplicateCheck;
echo ============================================
echo.
echo This shows you the total number of records that will be deleted.
echo.
pause
