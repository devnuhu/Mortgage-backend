using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MMGT.API.Migrations
{
    /// <inheritdoc />
    public partial class BackfillMissingApplicationAssignments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ReassignmentHistories",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ApplicationAssignmentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ApplicationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PreviousReviewerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    NewReviewerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ReassignmentReason = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    ReassignmentDetails = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    ReassignedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ReassignedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    PreviousAssignmentStatus = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    NewAssignmentStatus = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    IsCompleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReassignmentHistories", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ReassignmentHistories_ApplicationAssignments_ApplicationAssignmentId",
                        column: x => x.ApplicationAssignmentId,
                        principalTable: "ApplicationAssignments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ReassignmentHistories_Applications_ApplicationId",
                        column: x => x.ApplicationId,
                        principalTable: "Applications",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_ReassignmentHistories_Users_NewReviewerId",
                        column: x => x.NewReviewerId,
                        principalTable: "Users",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_ReassignmentHistories_Users_PreviousReviewerId",
                        column: x => x.PreviousReviewerId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ReassignmentHistories_Users_ReassignedByUserId",
                        column: x => x.ReassignedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ReviewerAvailabilities",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ReviewerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    StatusStartDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    StatusEndDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Reason = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    LastUpdated = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReviewerAvailabilities", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ReviewerAvailabilities_Users_ReviewerId",
                        column: x => x.ReviewerId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ReassignmentHistories_ApplicationAssignmentId",
                table: "ReassignmentHistories",
                column: "ApplicationAssignmentId");

            migrationBuilder.CreateIndex(
                name: "IX_ReassignmentHistories_ApplicationId",
                table: "ReassignmentHistories",
                column: "ApplicationId");

            migrationBuilder.CreateIndex(
                name: "IX_ReassignmentHistories_NewReviewerId",
                table: "ReassignmentHistories",
                column: "NewReviewerId");

            migrationBuilder.CreateIndex(
                name: "IX_ReassignmentHistories_PreviousReviewerId",
                table: "ReassignmentHistories",
                column: "PreviousReviewerId");

            migrationBuilder.CreateIndex(
                name: "IX_ReassignmentHistories_ReassignedAt",
                table: "ReassignmentHistories",
                column: "ReassignedAt",
                descending: new bool[0]);

            migrationBuilder.CreateIndex(
                name: "IX_ReassignmentHistories_ReassignedByUserId",
                table: "ReassignmentHistories",
                column: "ReassignedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ReviewerAvailabilities_ReviewerId",
                table: "ReviewerAvailabilities",
                column: "ReviewerId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ReassignmentHistories");

            migrationBuilder.DropTable(
                name: "ReviewerAvailabilities");
        }
    }
}
