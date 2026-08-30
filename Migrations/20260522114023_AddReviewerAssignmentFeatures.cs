using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MMGT.API.Migrations
{
    /// <inheritdoc />
    public partial class AddReviewerAssignmentFeatures : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PMICompanies",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PmiName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    EmailAddress = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    ResourcePersonName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    ResourcePersonEmail = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    PhoneNumber = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETDATE()"),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETDATE()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PMICompanies", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SystemConfigurations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ConfigKey = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ConfigValue = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    LastModifiedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LastModifiedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETDATE()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SystemConfigurations", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Users",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Email = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    FirstName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    LastName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Role = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    PasswordHash = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    IsFirstLogin = table.Column<bool>(type: "bit", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETDATE()"),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETDATE()"),
                    LastLoginAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    PMICompanyId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    PMIEmail = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    FirstLoginTokenHash = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    FirstLoginTokenExpiresAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Users", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Users_PMICompanies_PMICompanyId",
                        column: x => x.PMICompanyId,
                        principalTable: "PMICompanies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Applications",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ApplicationNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Priority = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    FirstName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    MiddleName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    LastName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    RsaPin = table.Column<string>(type: "nvarchar(15)", maxLength: 15, nullable: false),
                    EquityValue = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    SupplementaryValue = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    LoanAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    PropertyValue = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    PmiRepName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    PmiRepEmail = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    PmiCompanyName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETDATE()"),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETDATE()"),
                    SubmittedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DocumentStorageType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    MandatoryDocumentsComplete = table.Column<bool>(type: "bit", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PmiCompanyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Applications", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Applications_PMICompanies_PmiCompanyId",
                        column: x => x.PmiCompanyId,
                        principalTable: "PMICompanies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Applications_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "DocumentTypes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    IsMandatory = table.Column<bool>(type: "bit", nullable: false),
                    DisplayOrder = table.Column<int>(type: "int", nullable: false),
                    MaxFileSizeKB = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETDATE()"),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ModifiedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ModifiedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DocumentTypes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DocumentTypes_Users_CreatedBy",
                        column: x => x.CreatedBy,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DocumentTypes_Users_ModifiedBy",
                        column: x => x.ModifiedBy,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ApplicationAssignments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ApplicationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ReviewerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AssignedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    AssignedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETDATE()"),
                    ReassignmentReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ApplicationAssignments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ApplicationAssignments_Applications_ApplicationId",
                        column: x => x.ApplicationId,
                        principalTable: "Applications",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ApplicationAssignments_Users_AssignedBy",
                        column: x => x.AssignedBy,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ApplicationAssignments_Users_ReviewerId",
                        column: x => x.ReviewerId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ApplicationReviewHistory",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Comments = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETDATE()"),
                    ApplicationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ReviewedById = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ApplicationReviewHistory", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ApplicationReviewHistory_Applications_ApplicationId",
                        column: x => x.ApplicationId,
                        principalTable: "Applications",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ApplicationReviewHistory_Users_ReviewedById",
                        column: x => x.ReviewedById,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ApplicationDocuments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FileName = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    FilePath = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    FileContent = table.Column<byte[]>(type: "varbinary(max)", nullable: true),
                    FileType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    DocumentTypeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LegacyDocumentType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    FileSize = table.Column<long>(type: "bigint", nullable: false),
                    Version = table.Column<int>(type: "int", nullable: false),
                    IsLatestVersion = table.Column<bool>(type: "bit", nullable: false),
                    ReviewStatus = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    ReviewComments = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    ReviewedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ReviewedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETDATE()"),
                    UploadedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ApplicationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ApplicationDocuments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ApplicationDocuments_Applications_ApplicationId",
                        column: x => x.ApplicationId,
                        principalTable: "Applications",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ApplicationDocuments_DocumentTypes_DocumentTypeId",
                        column: x => x.DocumentTypeId,
                        principalTable: "DocumentTypes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ApplicationDocuments_Users_ReviewedBy",
                        column: x => x.ReviewedBy,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "DocumentTypeAuditLogs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DocumentTypeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FieldName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    OldValue = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    NewValue = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    ChangeType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    ChangeReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    ChangedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ChangedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETDATE()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DocumentTypeAuditLogs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DocumentTypeAuditLogs_DocumentTypes_DocumentTypeId",
                        column: x => x.DocumentTypeId,
                        principalTable: "DocumentTypes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_DocumentTypeAuditLogs_Users_ChangedBy",
                        column: x => x.ChangedBy,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ApplicationDocumentHistory",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ApplicationDocumentId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ApplicationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Action = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    PerformedById = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PerformedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETDATE()"),
                    OldFileName = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    NewFileName = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    Comments = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ApplicationDocumentHistory", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ApplicationDocumentHistory_ApplicationDocuments_ApplicationDocumentId",
                        column: x => x.ApplicationDocumentId,
                        principalTable: "ApplicationDocuments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ApplicationDocumentHistory_Applications_ApplicationId",
                        column: x => x.ApplicationId,
                        principalTable: "Applications",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ApplicationAssignments_ApplicationId_IsActive",
                table: "ApplicationAssignments",
                columns: new[] { "ApplicationId", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_ApplicationAssignments_AssignedBy",
                table: "ApplicationAssignments",
                column: "AssignedBy");

            migrationBuilder.CreateIndex(
                name: "IX_ApplicationAssignments_ReviewerId_IsActive",
                table: "ApplicationAssignments",
                columns: new[] { "ReviewerId", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_ApplicationDocumentHistory_ApplicationDocumentId",
                table: "ApplicationDocumentHistory",
                column: "ApplicationDocumentId");

            migrationBuilder.CreateIndex(
                name: "IX_ApplicationDocumentHistory_ApplicationId",
                table: "ApplicationDocumentHistory",
                column: "ApplicationId");

            migrationBuilder.CreateIndex(
                name: "IX_ApplicationDocuments_ApplicationId_DocumentTypeId_IsLatestVersion",
                table: "ApplicationDocuments",
                columns: new[] { "ApplicationId", "DocumentTypeId", "IsLatestVersion" });

            migrationBuilder.CreateIndex(
                name: "IX_ApplicationDocuments_ApplicationId_ReviewStatus",
                table: "ApplicationDocuments",
                columns: new[] { "ApplicationId", "ReviewStatus" });

            migrationBuilder.CreateIndex(
                name: "IX_ApplicationDocuments_DocumentTypeId",
                table: "ApplicationDocuments",
                column: "DocumentTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_ApplicationDocuments_ReviewedBy",
                table: "ApplicationDocuments",
                column: "ReviewedBy");

            migrationBuilder.CreateIndex(
                name: "IX_ApplicationReviewHistory_ApplicationId",
                table: "ApplicationReviewHistory",
                column: "ApplicationId");

            migrationBuilder.CreateIndex(
                name: "IX_ApplicationReviewHistory_ReviewedById",
                table: "ApplicationReviewHistory",
                column: "ReviewedById");

            migrationBuilder.CreateIndex(
                name: "IX_Applications_ApplicationNumber",
                table: "Applications",
                column: "ApplicationNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Applications_CreatedAt",
                table: "Applications",
                column: "CreatedAt",
                descending: new bool[0]);

            migrationBuilder.CreateIndex(
                name: "IX_Applications_PmiCompanyId_CreatedAt",
                table: "Applications",
                columns: new[] { "PmiCompanyId", "CreatedAt" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "IX_Applications_RsaPin",
                table: "Applications",
                column: "RsaPin");

            migrationBuilder.CreateIndex(
                name: "IX_Applications_Status",
                table: "Applications",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_Applications_Status_CreatedAt",
                table: "Applications",
                columns: new[] { "Status", "CreatedAt" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "IX_Applications_UserId_CreatedAt",
                table: "Applications",
                columns: new[] { "UserId", "CreatedAt" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "IX_DocumentTypeAuditLogs_ChangedBy",
                table: "DocumentTypeAuditLogs",
                column: "ChangedBy");

            migrationBuilder.CreateIndex(
                name: "IX_DocumentTypeAuditLogs_DocumentTypeId_ChangedAt",
                table: "DocumentTypeAuditLogs",
                columns: new[] { "DocumentTypeId", "ChangedAt" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "IX_DocumentTypes_Code",
                table: "DocumentTypes",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DocumentTypes_CreatedBy",
                table: "DocumentTypes",
                column: "CreatedBy");

            migrationBuilder.CreateIndex(
                name: "IX_DocumentTypes_IsActive_DisplayOrder",
                table: "DocumentTypes",
                columns: new[] { "IsActive", "DisplayOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_DocumentTypes_ModifiedBy",
                table: "DocumentTypes",
                column: "ModifiedBy");

            migrationBuilder.CreateIndex(
                name: "IX_PMICompanies_EmailAddress",
                table: "PMICompanies",
                column: "EmailAddress",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SystemConfigurations_ConfigKey",
                table: "SystemConfigurations",
                column: "ConfigKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Users_Email",
                table: "Users",
                column: "Email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Users_PMICompanyId",
                table: "Users",
                column: "PMICompanyId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ApplicationAssignments");

            migrationBuilder.DropTable(
                name: "ApplicationDocumentHistory");

            migrationBuilder.DropTable(
                name: "ApplicationReviewHistory");

            migrationBuilder.DropTable(
                name: "DocumentTypeAuditLogs");

            migrationBuilder.DropTable(
                name: "SystemConfigurations");

            migrationBuilder.DropTable(
                name: "ApplicationDocuments");

            migrationBuilder.DropTable(
                name: "Applications");

            migrationBuilder.DropTable(
                name: "DocumentTypes");

            migrationBuilder.DropTable(
                name: "Users");

            migrationBuilder.DropTable(
                name: "PMICompanies");
        }
    }
}
