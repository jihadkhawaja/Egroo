using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Egroo.Server.Migrations
{
    /// <inheritdoc />
    public partial class betterentitybase : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "isencrypted",
                table: "usersfriends");

            migrationBuilder.DropColumn(
                name: "isencrypted",
                table: "users");

            migrationBuilder.DropColumn(
                name: "isencrypted",
                table: "userfeedback");

            migrationBuilder.DropColumn(
                name: "isencrypted",
                table: "channelusers");

            migrationBuilder.DropColumn(
                name: "isencrypted",
                table: "channels");

            migrationBuilder.AlterColumn<int>(
                name: "id",
                table: "userfeedback",
                type: "integer",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uuid")
                .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
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

            migrationBuilder.AlterColumn<Guid>(
                name: "id",
                table: "userfeedback",
                type: "uuid",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer")
                .OldAnnotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn);

            migrationBuilder.AddColumn<bool>(
                name: "isencrypted",
                table: "userfeedback",
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
    }
}
