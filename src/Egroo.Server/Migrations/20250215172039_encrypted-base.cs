using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Egroo.Server.Migrations
{
    /// <inheritdoc />
    public partial class encryptedbase : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "isencrypted",
                table: "userspendingmessages",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "isencrypted",
                table: "usersfriends",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "isencrypted",
                table: "users",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "isencrypted",
                table: "messages",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "isencrypted",
                table: "channelusers",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "isencrypted",
                table: "channels",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "isencrypted",
                table: "userspendingmessages");

            migrationBuilder.DropColumn(
                name: "isencrypted",
                table: "usersfriends");

            migrationBuilder.DropColumn(
                name: "isencrypted",
                table: "users");

            migrationBuilder.DropColumn(
                name: "isencrypted",
                table: "messages");

            migrationBuilder.DropColumn(
                name: "isencrypted",
                table: "channelusers");

            migrationBuilder.DropColumn(
                name: "isencrypted",
                table: "channels");
        }
    }
}
