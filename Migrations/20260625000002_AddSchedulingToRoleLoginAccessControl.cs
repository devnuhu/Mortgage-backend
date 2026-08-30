using Microsoft.EntityFrameworkCore.Migrations;
using System;

#nullable disable

namespace MMGT.API.Migrations
{
    /// <inheritdoc />
    public partial class AddSchedulingToRoleLoginAccessControl : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsScheduled",
                table: "RoleLoginAccessControls",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "ScheduledEndTime",
                table: "RoleLoginAccessControls",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ScheduledReason",
                table: "RoleLoginAccessControls",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ScheduledStartTime",
                table: "RoleLoginAccessControls",
                type: "datetime2",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_RoleLoginAccessControls_IsScheduled_ScheduledStartTime_ScheduledEndTime",
                table: "RoleLoginAccessControls",
                columns: new[] { "IsScheduled", "ScheduledStartTime", "ScheduledEndTime" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_RoleLoginAccessControls_IsScheduled_ScheduledStartTime_ScheduledEndTime",
                table: "RoleLoginAccessControls");

            migrationBuilder.DropColumn(
                name: "IsScheduled",
                table: "RoleLoginAccessControls");

            migrationBuilder.DropColumn(
                name: "ScheduledEndTime",
                table: "RoleLoginAccessControls");

            migrationBuilder.DropColumn(
                name: "ScheduledReason",
                table: "RoleLoginAccessControls");

            migrationBuilder.DropColumn(
                name: "ScheduledStartTime",
                table: "RoleLoginAccessControls");
        }
    }
}
