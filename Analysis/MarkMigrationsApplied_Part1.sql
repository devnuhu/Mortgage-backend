-- Mark migrations as applied without running them
-- This handles the case where migrations were applied outside EF Core

USE MMGTDB2A;
GO

-- Add migration 1 to history (tables already exist)
INSERT INTO __EFMigrationsHistory (MigrationId, ProductVersion)
VALUES ('20260522114023_AddReviewerAssignmentFeatures', '8.0.19');

-- Add migration 2 to history (dedup was likely applied)
INSERT INTO __EFMigrationsHistory (MigrationId, ProductVersion)
VALUES ('20260601214604_DeduplicateApplicationsByRsaPin', '8.0.19');

-- Add migration 3 to history (if not already applied)
INSERT INTO __EFMigrationsHistory (MigrationId, ProductVersion)
VALUES ('20260601214605_AddRsaPinUniqueConstraint', '8.0.19');

-- Verify
SELECT MigrationId FROM __EFMigrationsHistory ORDER BY MigrationId;
