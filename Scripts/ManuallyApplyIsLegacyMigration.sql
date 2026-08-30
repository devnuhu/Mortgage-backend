-- Manual application of 20260625000000_AddIsLegacyToApplicationDocuments migration
-- This adds the IsLegacy column to ApplicationDocuments if it doesn't exist

IF NOT EXISTS (
	SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS 
	WHERE TABLE_NAME='ApplicationDocuments' AND COLUMN_NAME='IsLegacy'
)
BEGIN
	ALTER TABLE ApplicationDocuments 
	ADD IsLegacy bit NOT NULL DEFAULT 0;

	-- Update legacy documents (those with FilePath) to IsLegacy=1
	UPDATE ApplicationDocuments 
	SET IsLegacy = 1 
	WHERE FilePath IS NOT NULL AND FilePath != '';
END

-- Mark the migration as applied in the history
INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
SELECT N'20260625000000_AddIsLegacyToApplicationDocuments', N'8.0.19'
WHERE NOT EXISTS (
	SELECT 1 FROM [__EFMigrationsHistory] 
	WHERE [MigrationId] = N'20260625000000_AddIsLegacyToApplicationDocuments'
);
