using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MMGT.API.Migrations
{
    /// <inheritdoc />
    public partial class AddFirstLoginTokenFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "FirstLoginTokenExpiresAt",
                table: "Users",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FirstLoginTokenHash",
                table: "Users",
                type: "nvarchar(128)",
                maxLength: 128,
                nullable: true);
/*
            migrationBuilder.AddColumn<string>(
                name: "PMIEmail",
                table: "Users",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: true); */
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "FirstLoginTokenExpiresAt",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "FirstLoginTokenHash",
                table: "Users");

        }
    }
}
