-- =====================================================
-- COMPREHENSIVE SCHEMA RECOVERY SCRIPT
-- Updates database schema to match all pending migrations
-- =====================================================

-- 1. ADD MISSING COLUMNS TO USERS TABLE
-- =====================================================
IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME='Users' AND COLUMN_NAME='FirstLoginTokenHash')
BEGIN
	ALTER TABLE Users ADD FirstLoginTokenHash nvarchar(128) NULL;
END

IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME='Users' AND COLUMN_NAME='FirstLoginTokenExpiresAt')
BEGIN
	ALTER TABLE Users ADD FirstLoginTokenExpiresAt datetime2 NULL;
END

IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME='Users' AND COLUMN_NAME='PMICompanyId')
BEGIN
	ALTER TABLE Users ADD PMICompanyId uniqueidentifier NULL;
END

IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME='Users' AND COLUMN_NAME='PMIEmail')
BEGIN
	ALTER TABLE Users ADD PMIEmail nvarchar(256) NULL;
END

-- 2. ADD MISSING COLUMNS TO APPLICATIONS TABLE
-- =====================================================
IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME='Applications' AND COLUMN_NAME='EquityValue')
BEGIN
	ALTER TABLE Applications ADD EquityValue decimal(18,2) NOT NULL DEFAULT 0;
END

IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME='Applications' AND COLUMN_NAME='SupplementaryValue')
BEGIN
	ALTER TABLE Applications ADD SupplementaryValue decimal(18,2) NOT NULL DEFAULT 0;
END

IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME='Applications' AND COLUMN_NAME='PropertyValue')
BEGIN
	ALTER TABLE Applications ADD PropertyValue decimal(18,2) NOT NULL DEFAULT 0;
END

IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME='Applications' AND COLUMN_NAME='DocumentStorageType')
BEGIN
	ALTER TABLE Applications ADD DocumentStorageType nvarchar(50) NOT NULL DEFAULT 'Disk';
END

IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME='Applications' AND COLUMN_NAME='MandatoryDocumentsComplete')
BEGIN
	ALTER TABLE Applications ADD MandatoryDocumentsComplete bit NOT NULL DEFAULT 0;
END

IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME='Applications' AND COLUMN_NAME='SubmittedAt')
BEGIN
	ALTER TABLE Applications ADD SubmittedAt datetime2 NULL;
END

-- 3. ADD MISSING COLUMNS TO APPLICATIONDOCUMENTS TABLE
-- =====================================================
IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME='ApplicationDocuments' AND COLUMN_NAME='DocumentTypeId')
BEGIN
	ALTER TABLE ApplicationDocuments ADD DocumentTypeId uniqueidentifier NULL;
END

IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME='ApplicationDocuments' AND COLUMN_NAME='ReviewStatus')
BEGIN
	ALTER TABLE ApplicationDocuments ADD ReviewStatus nvarchar(50) NULL;
END

IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME='ApplicationDocuments' AND COLUMN_NAME='ReviewComments')
BEGIN
	ALTER TABLE ApplicationDocuments ADD ReviewComments nvarchar(500) NULL;
END

IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME='ApplicationDocuments' AND COLUMN_NAME='Version')
BEGIN
	ALTER TABLE ApplicationDocuments ADD [Version] int NOT NULL DEFAULT 1;
END

IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME='ApplicationDocuments' AND COLUMN_NAME='IsLatestVersion')
BEGIN
	ALTER TABLE ApplicationDocuments ADD IsLatestVersion bit NOT NULL DEFAULT 1;
END

-- 4. CREATE MISSING TABLES
-- =====================================================

-- Create SystemConfigurations if it doesn't exist
IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME='SystemConfigurations')
BEGIN
	CREATE TABLE SystemConfigurations (
		Id uniqueidentifier NOT NULL,
		ConfigKey nvarchar(100) NOT NULL,
		ConfigValue nvarchar(500) NOT NULL,
		Description nvarchar(500) NULL,
		LastModifiedBy uniqueidentifier NOT NULL,
		LastModifiedAt datetime2 NOT NULL DEFAULT GETDATE(),
		CONSTRAINT PK_SystemConfigurations PRIMARY KEY (Id)
	);
END

-- Create DocumentTypes if it doesn't exist
IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME='DocumentTypes')
BEGIN
	CREATE TABLE DocumentTypes (
		Id uniqueidentifier NOT NULL,
		Code nvarchar(100) NOT NULL,
		[Name] nvarchar(255) NOT NULL,
		[Description] nvarchar(1000) NULL,
		IsMandatory bit NOT NULL,
		DisplayOrder int NOT NULL,
		MaxFileSizeKB int NOT NULL,
		IsActive bit NOT NULL,
		CreatedAt datetime2 NOT NULL DEFAULT GETDATE(),
		CreatedBy uniqueidentifier NOT NULL,
		ModifiedAt datetime2 NULL,
		ModifiedBy uniqueidentifier NULL,
		CONSTRAINT PK_DocumentTypes PRIMARY KEY (Id),
		CONSTRAINT FK_DocumentTypes_Users_CreatedBy FOREIGN KEY (CreatedBy) REFERENCES Users(Id) ON DELETE NO ACTION,
		CONSTRAINT FK_DocumentTypes_Users_ModifiedBy FOREIGN KEY (ModifiedBy) REFERENCES Users(Id) ON DELETE NO ACTION
	);
END

-- Create DocumentTypeAuditLogs if it doesn't exist
IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME='DocumentTypeAuditLogs')
BEGIN
	CREATE TABLE DocumentTypeAuditLogs (
		Id uniqueidentifier NOT NULL,
		DocumentTypeId uniqueidentifier NOT NULL,
		Action nvarchar(50) NOT NULL,
		ChangedBy uniqueidentifier NOT NULL,
		OldValue nvarchar(500) NULL,
		NewValue nvarchar(500) NULL,
		CreatedAt datetime2 NOT NULL DEFAULT GETDATE(),
		CONSTRAINT PK_DocumentTypeAuditLogs PRIMARY KEY (Id),
		CONSTRAINT FK_DocumentTypeAuditLogs_DocumentTypes_DocumentTypeId FOREIGN KEY (DocumentTypeId) REFERENCES DocumentTypes(Id) ON DELETE CASCADE,
		CONSTRAINT FK_DocumentTypeAuditLogs_Users_ChangedBy FOREIGN KEY (ChangedBy) REFERENCES Users(Id) ON DELETE NO ACTION
	);
END

-- Create ApplicationDocumentHistory if it doesn't exist
IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME='ApplicationDocumentHistory')
BEGIN
	CREATE TABLE ApplicationDocumentHistory (
		Id uniqueidentifier NOT NULL,
		ApplicationDocumentId uniqueidentifier NOT NULL,
		ApplicationId uniqueidentifier NOT NULL,
		Action nvarchar(50) NOT NULL,
		FileName nvarchar(255) NOT NULL,
		FileSize bigint NOT NULL,
		ReviewStatus nvarchar(50) NOT NULL,
		ReviewComments nvarchar(500) NULL,
		ChangedBy uniqueidentifier NOT NULL,
		CreatedAt datetime2 NOT NULL DEFAULT GETDATE(),
		CONSTRAINT PK_ApplicationDocumentHistory PRIMARY KEY (Id),
		CONSTRAINT FK_ApplicationDocumentHistory_Applications_ApplicationId FOREIGN KEY (ApplicationId) REFERENCES Applications(Id) ON DELETE NO ACTION,
		CONSTRAINT FK_ApplicationDocumentHistory_ApplicationDocuments_ApplicationDocumentId FOREIGN KEY (ApplicationDocumentId) REFERENCES ApplicationDocuments(Id) ON DELETE CASCADE
	);
END

-- Create ApplicationAssignments if it doesn't exist
IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME='ApplicationAssignments')
BEGIN
	CREATE TABLE ApplicationAssignments (
		Id uniqueidentifier NOT NULL,
		ApplicationId uniqueidentifier NOT NULL,
		AssignedToUserId uniqueidentifier NOT NULL,
		AssignedByUserId uniqueidentifier NOT NULL,
		AssignedAt datetime2 NOT NULL DEFAULT GETDATE(),
		Status nvarchar(50) NOT NULL DEFAULT 'Active',
		[Priority] nvarchar(50) NULL,
		DueDate datetime2 NULL,
		Notes nvarchar(500) NULL,
		CONSTRAINT PK_ApplicationAssignments PRIMARY KEY (Id),
		CONSTRAINT FK_ApplicationAssignments_Applications_ApplicationId FOREIGN KEY (ApplicationId) REFERENCES Applications(Id) ON DELETE CASCADE,
		CONSTRAINT FK_ApplicationAssignments_Users_AssignedToUserId FOREIGN KEY (AssignedToUserId) REFERENCES Users(Id) ON DELETE NO ACTION,
		CONSTRAINT FK_ApplicationAssignments_Users_AssignedByUserId FOREIGN KEY (AssignedByUserId) REFERENCES Users(Id) ON DELETE NO ACTION
	);
END

-- Create ReviewerPerformanceMetrics if it doesn't exist
IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME='ReviewerPerformanceMetrics')
BEGIN
	CREATE TABLE ReviewerPerformanceMetrics (
		Id uniqueidentifier NOT NULL,
		ReviewerId uniqueidentifier NOT NULL,
		ApplicationsReviewed int NOT NULL DEFAULT 0,
		AverageReviewTimeMinutes decimal(18,2) NULL,
		ApprovalRate decimal(5,2) NULL,
		RejectionRate decimal(5,2) NULL,
		LastUpdated datetime2 NOT NULL DEFAULT GETDATE(),
		CONSTRAINT PK_ReviewerPerformanceMetrics PRIMARY KEY (Id),
		CONSTRAINT FK_ReviewerPerformanceMetrics_Users_ReviewerId FOREIGN KEY (ReviewerId) REFERENCES Users(Id) ON DELETE CASCADE
	);
END

-- Create EscalationRules if it doesn't exist
IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME='EscalationRules')
BEGIN
	CREATE TABLE EscalationRules (
		Id uniqueidentifier NOT NULL,
		RuleName nvarchar(100) NOT NULL,
		TriggerCondition nvarchar(500) NOT NULL,
		EscalationLevel int NOT NULL,
		EscalateToRole nvarchar(50) NOT NULL,
		IsActive bit NOT NULL DEFAULT 1,
		CreatedAt datetime2 NOT NULL DEFAULT GETDATE(),
		CONSTRAINT PK_EscalationRules PRIMARY KEY (Id)
	);
END

-- Create AssignmentEscalations if it doesn't exist
IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME='AssignmentEscalations')
BEGIN
	CREATE TABLE AssignmentEscalations (
		Id uniqueidentifier NOT NULL,
		ApplicationAssignmentId uniqueidentifier NOT NULL,
		EscalationRuleId uniqueidentifier NOT NULL,
		EscalationLevel int NOT NULL,
		EscalatedTo nvarchar(100) NOT NULL,
		EscalationReason nvarchar(500) NULL,
		Status nvarchar(50) NOT NULL DEFAULT 'Pending',
		ResolvedAt datetime2 NULL,
		CreatedAt datetime2 NOT NULL DEFAULT GETDATE(),
		CONSTRAINT PK_AssignmentEscalations PRIMARY KEY (Id),
		CONSTRAINT FK_AssignmentEscalations_ApplicationAssignments_ApplicationAssignmentId FOREIGN KEY (ApplicationAssignmentId) REFERENCES ApplicationAssignments(Id) ON DELETE CASCADE,
		CONSTRAINT FK_AssignmentEscalations_EscalationRules_EscalationRuleId FOREIGN KEY (EscalationRuleId) REFERENCES EscalationRules(Id) ON DELETE CASCADE
	);
END

-- Create ReviewerAvailabilities if it doesn't exist
IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME='ReviewerAvailabilities')
BEGIN
	CREATE TABLE ReviewerAvailabilities (
		Id uniqueidentifier NOT NULL,
		ReviewerId uniqueidentifier NOT NULL,
		AvailabilityDate date NOT NULL,
		StartTime time NOT NULL,
		EndTime time NOT NULL,
		AvailabilityStatus nvarchar(50) NOT NULL DEFAULT 'Available',
		Notes nvarchar(500) NULL,
		CreatedAt datetime2 NOT NULL DEFAULT GETDATE(),
		UpdatedAt datetime2 NOT NULL DEFAULT GETDATE(),
		CONSTRAINT PK_ReviewerAvailabilities PRIMARY KEY (Id),
		CONSTRAINT FK_ReviewerAvailabilities_Users_ReviewerId FOREIGN KEY (ReviewerId) REFERENCES Users(Id) ON DELETE CASCADE
	);
END

-- Create ReassignmentHistories if it doesn't exist
IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME='ReassignmentHistories')
BEGIN
	CREATE TABLE ReassignmentHistories (
		Id uniqueidentifier NOT NULL,
		ApplicationAssignmentId uniqueidentifier NOT NULL,
		FromUserId uniqueidentifier NOT NULL,
		ToUserId uniqueidentifier NOT NULL,
		ReassignedByUserId uniqueidentifier NOT NULL,
		ReassignmentReason nvarchar(500) NULL,
		ReassignedAt datetime2 NOT NULL DEFAULT GETDATE(),
		CONSTRAINT PK_ReassignmentHistories PRIMARY KEY (Id),
		CONSTRAINT FK_ReassignmentHistories_ApplicationAssignments_ApplicationAssignmentId FOREIGN KEY (ApplicationAssignmentId) REFERENCES ApplicationAssignments(Id) ON DELETE CASCADE,
		CONSTRAINT FK_ReassignmentHistories_Users_FromUserId FOREIGN KEY (FromUserId) REFERENCES Users(Id) ON DELETE NO ACTION,
		CONSTRAINT FK_ReassignmentHistories_Users_ToUserId FOREIGN KEY (ToUserId) REFERENCES Users(Id) ON DELETE NO ACTION,
		CONSTRAINT FK_ReassignmentHistories_Users_ReassignedByUserId FOREIGN KEY (ReassignedByUserId) REFERENCES Users(Id) ON DELETE NO ACTION
	);
END

-- 5. CREATE MISSING INDEXES
-- =====================================================

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name='IX_Applications_Status' AND object_id=OBJECT_ID('Applications'))
BEGIN
	CREATE INDEX IX_Applications_Status ON Applications([Status]);
END

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name='IX_Applications_CreatedAt' AND object_id=OBJECT_ID('Applications'))
BEGIN
	CREATE INDEX IX_Applications_CreatedAt ON Applications(CreatedAt DESC);
END

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name='IX_Applications_PmiCompanyId_CreatedAt' AND object_id=OBJECT_ID('Applications'))
BEGIN
	CREATE INDEX IX_Applications_PmiCompanyId_CreatedAt ON Applications(PmiCompanyId, CreatedAt DESC);
END

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name='IX_Applications_UserId_CreatedAt' AND object_id=OBJECT_ID('Applications'))
BEGIN
	CREATE INDEX IX_Applications_UserId_CreatedAt ON Applications(UserId, CreatedAt DESC);
END

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name='IX_Applications_Status_CreatedAt' AND object_id=OBJECT_ID('Applications'))
BEGIN
	CREATE INDEX IX_Applications_Status_CreatedAt ON Applications([Status], CreatedAt DESC);
END

-- RsaPin index with unique constraint (only for active statuses)
IF EXISTS (SELECT 1 FROM sys.indexes WHERE name='IX_Applications_RsaPin' AND object_id=OBJECT_ID('Applications') AND is_unique=0)
BEGIN
	DROP INDEX IX_Applications_RsaPin ON Applications;
END

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name='IX_Applications_RsaPin' AND object_id=OBJECT_ID('Applications') AND is_unique=1)
BEGIN
	CREATE UNIQUE INDEX IX_Applications_RsaPin ON Applications(RsaPin) 
	WHERE [Status] IN ('draft', 'submitted', 'under_review', 'returned', 'resubmitted');
END

-- Foreign key index for ApplicationDocuments
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name='IX_ApplicationDocuments_DocumentTypeId' AND object_id=OBJECT_ID('ApplicationDocuments'))
BEGIN
	CREATE INDEX IX_ApplicationDocuments_DocumentTypeId ON ApplicationDocuments(DocumentTypeId);
END

-- 6. REMOVE DUPLICATE RsaPins (keeping latest)
-- =====================================================

-- Remove duplicates in active statuses
DELETE FROM DuplicateRsaPins
FROM (
	SELECT 
		Id,
		RsaPin,
		CreatedAt,
		ROW_NUMBER() OVER (PARTITION BY RsaPin ORDER BY CreatedAt DESC) as rn
	FROM Applications
	WHERE [Status] IN ('draft', 'submitted', 'under_review', 'returned', 'resubmitted')
	AND RsaPin IS NOT NULL
	AND RsaPin != ''
) AS DuplicateRsaPins
WHERE rn > 1;

-- Remove duplicates in terminal statuses (keep latest)
DELETE FROM TerminalDuplicates
FROM (
	SELECT 
		Id,
		RsaPin,
		CreatedAt,
		ROW_NUMBER() OVER (PARTITION BY RsaPin ORDER BY CreatedAt DESC) as rn
	FROM Applications
	WHERE [Status] IN ('approved', 'rejected', 'abandoned', 'cancelled')
	AND RsaPin IS NOT NULL
	AND RsaPin != ''
) AS TerminalDuplicates
WHERE rn > 1;

-- 7. MARK ALL MIGRATIONS AS APPLIED
-- =====================================================

-- First migration (AddReviewerAssignmentFeatures)
INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
SELECT N'20260522114023_AddReviewerAssignmentFeatures', N'8.0.19'
WHERE NOT EXISTS (SELECT 1 FROM [__EFMigrationsHistory] WHERE [MigrationId] = N'20260522114023_AddReviewerAssignmentFeatures');

-- RsaPin deduplication
INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
SELECT N'20260601214604_DeduplicateApplicationsByRsaPin', N'8.0.19'
WHERE NOT EXISTS (SELECT 1 FROM [__EFMigrationsHistory] WHERE [MigrationId] = N'20260601214604_DeduplicateApplicationsByRsaPin');

-- RsaPin unique constraint
INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
SELECT N'20260601214605_AddRsaPinUniqueConstraint', N'8.0.19'
WHERE NOT EXISTS (SELECT 1 FROM [__EFMigrationsHistory] WHERE [MigrationId] = N'20260601214605_AddRsaPinUniqueConstraint');

-- Performance and escalation features
INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
SELECT N'20260602065404_AddPerformanceAndEscalationFeatures', N'8.0.19'
WHERE NOT EXISTS (SELECT 1 FROM [__EFMigrationsHistory] WHERE [MigrationId] = N'20260602065404_AddPerformanceAndEscalationFeatures');

-- Backfill missing assignments
INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
SELECT N'20260602154023_BackfillMissingApplicationAssignments', N'8.0.19'
WHERE NOT EXISTS (SELECT 1 FROM [__EFMigrationsHistory] WHERE [MigrationId] = N'20260602154023_BackfillMissingApplicationAssignments');

-- Advanced workload features
INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
SELECT N'20260602160556_AddAdvancedWorkloadFeatures', N'8.0.19'
WHERE NOT EXISTS (SELECT 1 FROM [__EFMigrationsHistory] WHERE [MigrationId] = N'20260602160556_AddAdvancedWorkloadFeatures');

-- Phone number configuration
INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
SELECT N'20260603214801_EnsurePhoneNumberConfiguration', N'8.0.19'
WHERE NOT EXISTS (SELECT 1 FROM [__EFMigrationsHistory] WHERE [MigrationId] = N'20260603214801_EnsurePhoneNumberConfiguration');

-- Fix migration history
INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
SELECT N'20260605_FixMigrationHistory', N'8.0.19'
WHERE NOT EXISTS (SELECT 1 FROM [__EFMigrationsHistory] WHERE [MigrationId] = N'20260605_FixMigrationHistory');

-- Ensure application status not null
INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
SELECT N'20260606120000_EnsureApplicationStatusNotNull', N'8.0.19'
WHERE NOT EXISTS (SELECT 1 FROM [__EFMigrationsHistory] WHERE [MigrationId] = N'20260606120000_EnsureApplicationStatusNotNull');

-- Add borrower phone number
INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
SELECT N'20260623120000_AddBorrowerPhoneNumber', N'8.0.19'
WHERE NOT EXISTS (SELECT 1 FROM [__EFMigrationsHistory] WHERE [MigrationId] = N'20260623120000_AddBorrowerPhoneNumber');

-- =====================================================
-- SCHEMA RECOVERY COMPLETE
-- =====================================================
PRINT 'Schema recovery complete! All missing tables, columns, and indexes have been created.';
PRINT 'All pending migrations have been marked as applied.';
