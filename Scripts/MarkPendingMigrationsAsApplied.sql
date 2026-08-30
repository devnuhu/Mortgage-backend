-- Mark all pending migrations as applied without executing them
-- These migrations attempt to create tables that already exist in the restored DB

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
SELECT N'20260522114023_AddReviewerAssignmentFeatures', N'8.0.19'
WHERE NOT EXISTS (SELECT 1 FROM [__EFMigrationsHistory] WHERE [MigrationId] = N'20260522114023_AddReviewerAssignmentFeatures');

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
SELECT N'20260601214604_DeduplicateApplicationsByRsaPin', N'8.0.19'
WHERE NOT EXISTS (SELECT 1 FROM [__EFMigrationsHistory] WHERE [MigrationId] = N'20260601214604_DeduplicateApplicationsByRsaPin');

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
SELECT N'20260601214605_AddRsaPinUniqueConstraint', N'8.0.19'
WHERE NOT EXISTS (SELECT 1 FROM [__EFMigrationsHistory] WHERE [MigrationId] = N'20260601214605_AddRsaPinUniqueConstraint');

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
SELECT N'20260602065404_AddPerformanceAndEscalationFeatures', N'8.0.19'
WHERE NOT EXISTS (SELECT 1 FROM [__EFMigrationsHistory] WHERE [MigrationId] = N'20260602065404_AddPerformanceAndEscalationFeatures');

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
SELECT N'20260602154023_BackfillMissingApplicationAssignments', N'8.0.19'
WHERE NOT EXISTS (SELECT 1 FROM [__EFMigrationsHistory] WHERE [MigrationId] = N'20260602154023_BackfillMissingApplicationAssignments');

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
SELECT N'20260602160556_AddAdvancedWorkloadFeatures', N'8.0.19'
WHERE NOT EXISTS (SELECT 1 FROM [__EFMigrationsHistory] WHERE [MigrationId] = N'20260602160556_AddAdvancedWorkloadFeatures');

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
SELECT N'20260603214801_EnsurePhoneNumberConfiguration', N'8.0.19'
WHERE NOT EXISTS (SELECT 1 FROM [__EFMigrationsHistory] WHERE [MigrationId] = N'20260603214801_EnsurePhoneNumberConfiguration');

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
SELECT N'20260605_FixMigrationHistory', N'8.0.19'
WHERE NOT EXISTS (SELECT 1 FROM [__EFMigrationsHistory] WHERE [MigrationId] = N'20260605_FixMigrationHistory');

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
SELECT N'20260606120000_EnsureApplicationStatusNotNull', N'8.0.19'
WHERE NOT EXISTS (SELECT 1 FROM [__EFMigrationsHistory] WHERE [MigrationId] = N'20260606120000_EnsureApplicationStatusNotNull');

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
SELECT N'20260623120000_AddBorrowerPhoneNumber', N'8.0.19'
WHERE NOT EXISTS (SELECT 1 FROM [__EFMigrationsHistory] WHERE [MigrationId] = N'20260623120000_AddBorrowerPhoneNumber');

-- Now safe to apply our minimal IsLegacy migration
