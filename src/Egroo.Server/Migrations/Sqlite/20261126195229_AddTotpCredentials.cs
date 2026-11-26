using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Egroo.Server.Migrations.Sqlite
{
    /// <inheritdoc />
    public partial class AddTotpCredentials : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "factorlockeduntil",
                table: "usersecurity",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "factorrevision",
                table: "usersecurity",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<int>(
                name: "failedfactorattempts",
                table: "usersecurity",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<long>(
                name: "lasttotpstep",
                table: "usersecurity",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "protectedtotpsecret",
                table: "usersecurity",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "recoverycodehashes",
                table: "usersecurity",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "totpenrollmentexpiresat",
                table: "usersecurity",
                type: "INTEGER",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "factorlockeduntil",
                table: "usersecurity");

            migrationBuilder.DropColumn(
                name: "factorrevision",
                table: "usersecurity");

            migrationBuilder.DropColumn(
                name: "failedfactorattempts",
                table: "usersecurity");

            migrationBuilder.DropColumn(
                name: "lasttotpstep",
                table: "usersecurity");

            migrationBuilder.DropColumn(
                name: "protectedtotpsecret",
                table: "usersecurity");

            migrationBuilder.DropColumn(
                name: "recoverycodehashes",
                table: "usersecurity");

            migrationBuilder.DropColumn(
                name: "totpenrollmentexpiresat",
                table: "usersecurity");
        }
    }
}
