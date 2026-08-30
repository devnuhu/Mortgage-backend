using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MMGT.API.Migrations
{
    /// <inheritdoc />
    public partial class AddApplicationStatusIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_Applications_Status",
                table: "Applications",
                column: "Status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Applications_Status",
                table: "Applications");
        }
    }
}
