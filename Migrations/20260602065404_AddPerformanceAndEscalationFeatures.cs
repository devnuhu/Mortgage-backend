using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MMGT.API.Migrations
{
    /// <inheritdoc />
    public partial class AddPerformanceAndEscalationFeatures : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "CompletedAt",
                table: "Applications",
                type: "datetime2",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "EscalationRules",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RuleName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    ThresholdHours = table.Column<int>(type: "int", nullable: false),
                    ApplicablePriority = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    EscalationAction = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    NotifyEmails = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModifiedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ModifiedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EscalationRules", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EscalationRules_Users_CreatedBy",
                        column: x => x.CreatedBy,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_EscalationRules_Users_ModifiedBy",
                        column: x => x.ModifiedBy,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ReviewerPerformanceMetrics",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ReviewerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TotalAssignments = table.Column<int>(type: "int", nullable: false),
                    ActiveAssignments = table.Column<int>(type: "int", nullable: false),
                    CompletedAssignments = table.Column<int>(type: "int", nullable: false),
                    DocumentsApproved = table.Column<int>(type: "int", nullable: false),
                    DocumentsRejected = table.Column<int>(type: "int", nullable: false),
                    DocumentsRevisionRequested = table.Column<int>(type: "int", nullable: false),
                    AverageTurnaroundTimeHours = table.Column<double>(type: "float", nullable: false),
                    ApprovalRatePercentage = table.Column<double>(type: "float", nullable: false),
                    RejectionRatePercentage = table.Column<double>(type: "float", nullable: false),
                    RevisionRequestRatePercentage = table.Column<double>(type: "float", nullable: false),
                    EscalatedAssignments = table.Column<int>(type: "int", nullable: false),
                    TotalEscalationCount = table.Column<int>(type: "int", nullable: false),
                    LastCalculatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    PerformanceRating = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    TotalHoursSpent = table.Column<double>(type: "float", nullable: false),
                    AverageHoursPerAssignment = table.Column<double>(type: "float", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReviewerPerformanceMetrics", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ReviewerPerformanceMetrics_Users_ReviewerId",
                        column: x => x.ReviewerId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ReviewerPerformanceMetrics_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "AssignmentEscalations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ApplicationAssignmentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EscalationRuleId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EscalatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    HoursOverThreshold = table.Column<double>(type: "float", nullable: false),
                    ReassignedToReviewerId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ActionReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    ResolvedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ResolutionComments = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    ResolvedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AssignmentEscalations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AssignmentEscalations_ApplicationAssignments_ApplicationAssignmentId",
                        column: x => x.ApplicationAssignmentId,
                        principalTable: "ApplicationAssignments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AssignmentEscalations_EscalationRules_EscalationRuleId",
                        column: x => x.EscalationRuleId,
                        principalTable: "EscalationRules",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AssignmentEscalations_Users_ReassignedToReviewerId",
                        column: x => x.ReassignedToReviewerId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AssignmentEscalations_Users_ResolvedBy",
                        column: x => x.ResolvedBy,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AssignmentEscalations_ApplicationAssignmentId_Status",
                table: "AssignmentEscalations",
                columns: new[] { "ApplicationAssignmentId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_AssignmentEscalations_EscalatedAt",
                table: "AssignmentEscalations",
                column: "EscalatedAt",
                descending: new bool[0]);

            migrationBuilder.CreateIndex(
                name: "IX_AssignmentEscalations_EscalationRuleId_Status",
                table: "AssignmentEscalations",
                columns: new[] { "EscalationRuleId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_AssignmentEscalations_ReassignedToReviewerId",
                table: "AssignmentEscalations",
                column: "ReassignedToReviewerId");

            migrationBuilder.CreateIndex(
                name: "IX_AssignmentEscalations_ResolvedBy",
                table: "AssignmentEscalations",
                column: "ResolvedBy");

            migrationBuilder.CreateIndex(
                name: "IX_AssignmentEscalations_Status",
                table: "AssignmentEscalations",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_EscalationRules_ApplicablePriority_IsActive",
                table: "EscalationRules",
                columns: new[] { "ApplicablePriority", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_EscalationRules_CreatedBy",
                table: "EscalationRules",
                column: "CreatedBy");

            migrationBuilder.CreateIndex(
                name: "IX_EscalationRules_IsActive",
                table: "EscalationRules",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_EscalationRules_ModifiedBy",
                table: "EscalationRules",
                column: "ModifiedBy");

            migrationBuilder.CreateIndex(
                name: "IX_ReviewerPerformanceMetrics_ApprovalRatePercentage_LastCalculatedAt",
                table: "ReviewerPerformanceMetrics",
                columns: new[] { "ApprovalRatePercentage", "LastCalculatedAt" },
                descending: new bool[0]);

            migrationBuilder.CreateIndex(
                name: "IX_ReviewerPerformanceMetrics_ReviewerId",
                table: "ReviewerPerformanceMetrics",
                column: "ReviewerId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ReviewerPerformanceMetrics_UserId",
                table: "ReviewerPerformanceMetrics",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AssignmentEscalations");

            migrationBuilder.DropTable(
                name: "ReviewerPerformanceMetrics");

            migrationBuilder.DropTable(
                name: "EscalationRules");

            migrationBuilder.DropColumn(
                name: "CompletedAt",
                table: "Applications");
        }
    }
}
