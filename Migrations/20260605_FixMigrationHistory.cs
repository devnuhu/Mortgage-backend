using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MMGT.API.Migrations
{
    /// <inheritdoc />
    public partial class FixMigrationHistory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // This migration is a no-op that exists solely to resolve the migration history
            // The ReassignmentHistories and ReviewerAvailabilities tables were already created
            // by migration 20260602154023_BackfillMissingApplicationAssignments but were not
            // recorded in the __EFMigrationsHistory table, causing the migration to be reapplied.
            //
            // This migration marks them as applied without recreating the tables.
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // This is a history-only migration, so Down is empty
        }
    }
}
