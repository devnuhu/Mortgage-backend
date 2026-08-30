using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MMGT.API.Migrations
{
    /// <inheritdoc />
    public partial class EnsureApplicationStatusNotNull : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Backfill any NULL Status values with 'draft' as default
            migrationBuilder.Sql(@"
                UPDATE [Applications]
                SET [Status] = 'draft'
                WHERE [Status] IS NULL OR [Status] = ''
            ");

            // Ensure all Status values follow the application workflow
            // If an application has been submitted, set status appropriately
            migrationBuilder.Sql(@"
                UPDATE [Applications]
                SET [Status] = 'submitted'
                WHERE [SubmittedAt] IS NOT NULL 
                AND [Status] = 'draft'
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // No down migration needed - we're just ensuring data consistency
        }
    }
}
