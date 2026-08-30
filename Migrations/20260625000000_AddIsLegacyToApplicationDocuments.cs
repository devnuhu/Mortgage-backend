using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MMGT.API.Migrations
{
    /// <inheritdoc />
    public partial class AddIsLegacyToApplicationDocuments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Use raw SQL with IF NOT EXISTS to make it idempotent
            migrationBuilder.Sql(@"
                IF NOT EXISTS (
                    SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS 
                    WHERE TABLE_NAME='ApplicationDocuments' AND COLUMN_NAME='IsLegacy'
                )
                BEGIN
                    ALTER TABLE ApplicationDocuments 
                    ADD IsLegacy bit NOT NULL DEFAULT 0;
                END
            ", suppressTransaction: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsLegacy",
                table: "ApplicationDocuments");
        }
    }
}
