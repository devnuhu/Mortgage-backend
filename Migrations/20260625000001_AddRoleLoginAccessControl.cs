using Microsoft.EntityFrameworkCore.Migrations;
using System;

#nullable disable

namespace MMGT.API.Migrations
{
    /// <inheritdoc />
    public partial class AddRoleLoginAccessControl : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "RoleLoginAccessControls",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Role = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    IsLoginEnabled = table.Column<bool>(type: "bit", nullable: false),
                    DisabledMessage = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    ChangeReason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    LastModifiedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LastModifiedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETDATE()"),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETDATE()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RoleLoginAccessControls", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_RoleLoginAccessControls_Role",
                table: "RoleLoginAccessControls",
                column: "Role",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RoleLoginAccessControls");
        }
    }
}
