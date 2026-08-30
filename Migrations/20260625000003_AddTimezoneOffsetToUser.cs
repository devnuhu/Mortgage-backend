using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MMGT.API.Migrations
{
    /// <inheritdoc />
    public partial class AddTimezoneOffsetToUser : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "TimezoneOffsetHours",
                table: "Users",
                type: "int",
                nullable: false,
                defaultValue: 1);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "TimezoneOffsetHours",
                table: "Users");
        }
    }
}
