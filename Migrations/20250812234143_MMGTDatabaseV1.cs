using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MMGT.API.Migrations
{
    /// <inheritdoc />
    public partial class MMGTDatabaseV1 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Create PMICompanies table if it doesn't exist
            migrationBuilder.Sql(@"
                IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'PMICompanies')
                BEGIN
                    CREATE TABLE [PMICompanies] (
                        [Id] uniqueidentifier NOT NULL,
                        [PmiName] nvarchar(200) NOT NULL,
                        [EmailAddress] nvarchar(256) NOT NULL,
                        [ResourcePersonName] nvarchar(200) NOT NULL,
                        [ResourcePersonEmail] nvarchar(256) NOT NULL,
                        [PhoneNumber] nvarchar(20) NOT NULL,
                        [Status] nvarchar(50) NOT NULL,
                        [CreatedAt] datetime2 NOT NULL DEFAULT GETDATE(),
                        [UpdatedAt] datetime2 NOT NULL DEFAULT GETDATE(),
                        CONSTRAINT [PK_PMICompanies] PRIMARY KEY ([Id])
                    );

                    CREATE UNIQUE INDEX [IX_PMICompanies_EmailAddress] ON [PMICompanies]([EmailAddress]);
                END
            ");

            // Create Users table if it doesn't exist
            migrationBuilder.Sql(@"
                IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'Users')
                BEGIN
                    CREATE TABLE [Users] (
                        [Id] uniqueidentifier NOT NULL,
                        [Email] nvarchar(256) NOT NULL,
                        [FirstName] nvarchar(100) NOT NULL,
                        [LastName] nvarchar(100) NOT NULL,
                        [Role] nvarchar(50) NOT NULL,
                        [PasswordHash] nvarchar(max) NOT NULL,
                        [IsFirstLogin] bit NOT NULL,
                        [IsActive] bit NOT NULL,
                        [CreatedAt] datetime2 NOT NULL DEFAULT GETDATE(),
                        [UpdatedAt] datetime2 NOT NULL DEFAULT GETDATE(),
                        [LastLoginAt] datetime2 NULL,
                        [PMICompanyId] uniqueidentifier NULL,
                        CONSTRAINT [PK_Users] PRIMARY KEY ([Id]),
                        CONSTRAINT [FK_Users_PMICompanies_PMICompanyId] FOREIGN KEY ([PMICompanyId]) 
                            REFERENCES [PMICompanies]([Id]) ON DELETE NO ACTION
                    );

                    CREATE UNIQUE INDEX [IX_Users_Email] ON [Users]([Email]);
                    CREATE INDEX [IX_Users_PMICompanyId] ON [Users]([PMICompanyId]);
                END
            ");

            // Create Applications table if it doesn't exist
            migrationBuilder.Sql(@"
                IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'Applications')
                BEGIN
                    CREATE TABLE [Applications] (
                        [Id] uniqueidentifier NOT NULL,
                        [ApplicationNumber] nvarchar(50) NOT NULL,
                        [Status] nvarchar(50) NOT NULL,
                        [Priority] nvarchar(50) NOT NULL,
                        [FirstName] nvarchar(100) NOT NULL,
                        [MiddleName] nvarchar(100) NULL,
                        [LastName] nvarchar(100) NOT NULL,
                        [RsaPin] nvarchar(15) NOT NULL,
                        [EquityValue] decimal(18,2) NOT NULL,
                        [SupplementaryValue] decimal(18,2) NOT NULL,
                        [LoanAmount] decimal(18,2) NOT NULL,
                        [PropertyValue] decimal(18,2) NOT NULL,
                        [PmiRepName] nvarchar(200) NOT NULL,
                        [PmiRepEmail] nvarchar(256) NOT NULL,
                        [PmiCompanyName] nvarchar(200) NOT NULL,
                        [CreatedAt] datetime2 NOT NULL DEFAULT GETDATE(),
                        [UpdatedAt] datetime2 NOT NULL DEFAULT GETDATE(),
                        [SubmittedAt] datetime2 NULL,
                        [UserId] uniqueidentifier NOT NULL,
                        [PmiCompanyId] uniqueidentifier NOT NULL,
                        CONSTRAINT [PK_Applications] PRIMARY KEY ([Id]),
                        CONSTRAINT [FK_Applications_PMICompanies_PmiCompanyId] FOREIGN KEY ([PmiCompanyId]) 
                            REFERENCES [PMICompanies]([Id]) ON DELETE NO ACTION,
                        CONSTRAINT [FK_Applications_Users_UserId] FOREIGN KEY ([UserId]) 
                            REFERENCES [Users]([Id]) ON DELETE NO ACTION
                    );

                    CREATE UNIQUE INDEX [IX_Applications_ApplicationNumber] ON [Applications]([ApplicationNumber]);
                    CREATE INDEX [IX_Applications_Status] ON [Applications]([Status]);
                    CREATE INDEX [IX_Applications_UserId_CreatedAt] ON [Applications]([UserId], [CreatedAt] DESC);
                    CREATE INDEX [IX_Applications_PmiCompanyId_CreatedAt] ON [Applications]([PmiCompanyId], [CreatedAt] DESC);
                    CREATE INDEX [IX_Applications_Status_CreatedAt] ON [Applications]([Status], [CreatedAt] DESC);
                    CREATE INDEX [IX_Applications_CreatedAt] ON [Applications]([CreatedAt] DESC);
                END
            ");

            // Create ApplicationDocuments table if it doesn't exist
            migrationBuilder.Sql(@"
                IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'ApplicationDocuments')
                BEGIN
                    CREATE TABLE [ApplicationDocuments] (
                        [Id] uniqueidentifier NOT NULL,
                        [FileName] nvarchar(255) NOT NULL,
                        [FilePath] nvarchar(500) NOT NULL,
                        [FileType] nvarchar(100) NOT NULL,
                        [DocumentType] nvarchar(100) NOT NULL,
                        [FileSize] bigint NOT NULL,
                        [CreatedAt] datetime2 NOT NULL DEFAULT GETDATE(),
                        [ApplicationId] uniqueidentifier NOT NULL,
                        CONSTRAINT [PK_ApplicationDocuments] PRIMARY KEY ([Id]),
                        CONSTRAINT [FK_ApplicationDocuments_Applications_ApplicationId] FOREIGN KEY ([ApplicationId]) 
                            REFERENCES [Applications]([Id]) ON DELETE CASCADE
                    );

                    CREATE INDEX [IX_ApplicationDocuments_ApplicationId] ON [ApplicationDocuments]([ApplicationId]);
                END
            ");

            // Create ApplicationReviewHistory table if it doesn't exist
            migrationBuilder.Sql(@"
                IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'ApplicationReviewHistory')
                BEGIN
                    CREATE TABLE [ApplicationReviewHistory] (
                        [Id] uniqueidentifier NOT NULL,
                        [Status] nvarchar(50) NOT NULL,
                        [Comments] nvarchar(1000) NULL,
                        [CreatedAt] datetime2 NOT NULL DEFAULT GETDATE(),
                        [ApplicationId] uniqueidentifier NOT NULL,
                        [ReviewedById] uniqueidentifier NOT NULL,
                        CONSTRAINT [PK_ApplicationReviewHistory] PRIMARY KEY ([Id]),
                        CONSTRAINT [FK_ApplicationReviewHistory_Applications_ApplicationId] FOREIGN KEY ([ApplicationId]) 
                            REFERENCES [Applications]([Id]) ON DELETE CASCADE,
                        CONSTRAINT [FK_ApplicationReviewHistory_Users_ReviewedById] FOREIGN KEY ([ReviewedById]) 
                            REFERENCES [Users]([Id]) ON DELETE NO ACTION
                    );

                    CREATE INDEX [IX_ApplicationReviewHistory_ApplicationId] ON [ApplicationReviewHistory]([ApplicationId]);
                    CREATE INDEX [IX_ApplicationReviewHistory_ReviewedById] ON [ApplicationReviewHistory]([ReviewedById]);
                END
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ApplicationReviewHistory");

            migrationBuilder.DropTable(
                name: "ApplicationDocuments");

            migrationBuilder.DropTable(
                name: "Applications");

            migrationBuilder.DropTable(
                name: "Users");

            migrationBuilder.DropTable(
                name: "PMICompanies");
        }
    }
}
