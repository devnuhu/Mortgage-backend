using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using MMGT.API.Data;

#nullable disable

namespace MMGT.API.Migrations
{
    /// <summary>
    /// Step-down foundation. Hand-written and fully guarded (IF NOT EXISTS): applying it twice, or after the
    /// SQL script has been run, changes nothing. Down is empty on purpose: this release never drops data.
    /// </summary>
    [DbContext(typeof(ApplicationDbContext))]
    [Migration("20261112090000_AddApplicationStepDown")]
    public partial class AddApplicationStepDown : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
IF OBJECT_ID(N'dbo.Applications', N'U') IS NULL OR OBJECT_ID(N'dbo.Users', N'U') IS NULL
    THROW 50001, N'dbo.Applications or dbo.Users was not found. Nothing was changed.', 1;

IF COL_LENGTH(N'dbo.Applications', N'IsLocked') IS NULL
    ALTER TABLE dbo.Applications ADD IsLocked bit NOT NULL CONSTRAINT DF_Applications_IsLocked DEFAULT (0);

IF COL_LENGTH(N'dbo.Applications', N'IsArchived') IS NULL
    ALTER TABLE dbo.Applications ADD IsArchived bit NOT NULL CONSTRAINT DF_Applications_IsArchived DEFAULT (0);

IF OBJECT_ID(N'dbo.ApplicationStepDownRequests', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.ApplicationStepDownRequests (
        Id uniqueidentifier NOT NULL CONSTRAINT PK_ApplicationStepDownRequests PRIMARY KEY,
        ApplicationId uniqueidentifier NOT NULL,
        Status nvarchar(30) NOT NULL,
        Reason nvarchar(1000) NOT NULL,
        PmiCompanyName nvarchar(200) NOT NULL,
        RequestedById uniqueidentifier NOT NULL,
        CreatedAt datetime2 NOT NULL,
        SubmittedAt datetime2 NULL,
        CancellationRequestedAt datetime2 NULL,
        CancelReason nvarchar(1000) NULL,
        DecidedById uniqueidentifier NULL,
        DecidedAt datetime2 NULL,
        DecisionComment nvarchar(1000) NULL,
        ActionedById uniqueidentifier NULL,
        ActionedAt datetime2 NULL,
        ActionComment nvarchar(1000) NULL,
        RestoredById uniqueidentifier NULL,
        RestoredAt datetime2 NULL,
        RestoreReason nvarchar(1000) NULL,
        RowVersion rowversion NOT NULL,
        CONSTRAINT FK_StepDownRequests_Applications FOREIGN KEY (ApplicationId) REFERENCES dbo.Applications (Id)
    );
END

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_StepDownRequests_ApplicationId_Status' AND object_id = OBJECT_ID(N'dbo.ApplicationStepDownRequests'))
    EXEC(N'CREATE INDEX IX_StepDownRequests_ApplicationId_Status ON dbo.ApplicationStepDownRequests (ApplicationId, Status)');

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_StepDownRequests_Status_CreatedAt' AND object_id = OBJECT_ID(N'dbo.ApplicationStepDownRequests'))
    EXEC(N'CREATE INDEX IX_StepDownRequests_Status_CreatedAt ON dbo.ApplicationStepDownRequests (Status, CreatedAt)');

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'UX_StepDownRequests_OpenPerApplication' AND object_id = OBJECT_ID(N'dbo.ApplicationStepDownRequests'))
    EXEC(N'CREATE UNIQUE INDEX UX_StepDownRequests_OpenPerApplication ON dbo.ApplicationStepDownRequests (ApplicationId) WHERE Status IN (''Draft'',''Submitted'',''CancellationRequested'',''Approved'')');

IF OBJECT_ID(N'dbo.ApplicationStepDownAudits', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.ApplicationStepDownAudits (
        Id uniqueidentifier NOT NULL CONSTRAINT PK_ApplicationStepDownAudits PRIMARY KEY,
        RequestId uniqueidentifier NOT NULL,
        ApplicationId uniqueidentifier NOT NULL,
        [Action] nvarchar(50) NOT NULL,
        FromStatus nvarchar(30) NULL,
        ToStatus nvarchar(30) NULL,
        PerformedById uniqueidentifier NOT NULL,
        PerformedByName nvarchar(200) NOT NULL,
        PerformedByRole nvarchar(50) NOT NULL,
        PerformedAt datetime2 NOT NULL,
        Comment nvarchar(1000) NULL,
        Details nvarchar(500) NULL,
        CONSTRAINT FK_StepDownAudits_Requests FOREIGN KEY (RequestId) REFERENCES dbo.ApplicationStepDownRequests (Id),
        CONSTRAINT FK_StepDownAudits_Applications FOREIGN KEY (ApplicationId) REFERENCES dbo.Applications (Id)
    );
END

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_StepDownAudits_ApplicationId_PerformedAt' AND object_id = OBJECT_ID(N'dbo.ApplicationStepDownAudits'))
    EXEC(N'CREATE INDEX IX_StepDownAudits_ApplicationId_PerformedAt ON dbo.ApplicationStepDownAudits (ApplicationId, PerformedAt)');

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_StepDownAudits_RequestId_PerformedAt' AND object_id = OBJECT_ID(N'dbo.ApplicationStepDownAudits'))
    EXEC(N'CREATE INDEX IX_StepDownAudits_RequestId_PerformedAt ON dbo.ApplicationStepDownAudits (RequestId, PerformedAt)');
");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
        }
    }
}
