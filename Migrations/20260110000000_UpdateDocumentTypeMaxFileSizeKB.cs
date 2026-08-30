using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MMGT.API.Migrations
{
    /// <inheritdoc />
    public partial class UpdateDocumentTypeMaxFileSizeKB : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Update all DocumentTypes to have 500KB limit by default
            // PROPERTY_TITLE_DOCUMENT gets 1024KB
            migrationBuilder.Sql(@"
                UPDATE DocumentTypes 
                SET MaxFileSizeKB = 500 
                WHERE Code != 'PROPERTY_TITLE_DOCUMENT' AND MaxFileSizeKB != 500;

                UPDATE DocumentTypes 
                SET MaxFileSizeKB = 1024 
                WHERE Code = 'PROPERTY_TITLE_DOCUMENT' AND MaxFileSizeKB != 1024;
            ", suppressTransaction: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // This is a data-only migration, rolling back would require knowing original values
            // For now, we'll skip Down as it's safer to keep the corrected values
            migrationBuilder.Sql("-- Down migration skipped for data correction. Review and manual rollback if needed.");
        }
    }
}
