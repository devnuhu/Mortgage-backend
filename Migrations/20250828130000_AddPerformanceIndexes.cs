using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MMGT.API.Migrations
{
    /// <inheritdoc />
    public partial class AddPerformanceIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_Applications_CreatedAt",
                table: "Applications",
                column: "CreatedAt",
                descending: new bool[] { true });

            migrationBuilder.CreateIndex(
                name: "IX_Applications_PmiCompanyId_CreatedAt",
                table: "Applications",
                columns: new[] { "PmiCompanyId", "CreatedAt" },
                descending: new bool[] { false, true });

            migrationBuilder.CreateIndex(
                name: "IX_Applications_RsaPin",
                table: "Applications",
                column: "RsaPin");

            migrationBuilder.CreateIndex(
                name: "IX_Applications_Status_CreatedAt",
                table: "Applications",
                columns: new[] { "Status", "CreatedAt" },
                descending: new bool[] { false, true });

            migrationBuilder.CreateIndex(
                name: "IX_Applications_UserId_CreatedAt",
                table: "Applications",
                columns: new[] { "UserId", "CreatedAt" },
                descending: new bool[] { false, true });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Applications_CreatedAt",
                table: "Applications");

            migrationBuilder.DropIndex(
                name: "IX_Applications_PmiCompanyId_CreatedAt",
                table: "Applications");

            migrationBuilder.DropIndex(
                name: "IX_Applications_RsaPin",
                table: "Applications");

            migrationBuilder.DropIndex(
                name: "IX_Applications_Status_CreatedAt",
                table: "Applications");

            migrationBuilder.DropIndex(
                name: "IX_Applications_UserId_CreatedAt",
                table: "Applications");
        }
    }
}
