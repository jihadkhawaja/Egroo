using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Egroo.Server.Migrations
{
    /// <inheritdoc />
    public partial class publicchannel : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "createdby",
                table: "userspendingmessages",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "deletedby",
                table: "userspendingmessages",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "updatedby",
                table: "userspendingmessages",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "createdby",
                table: "usersfriends",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "deletedby",
                table: "usersfriends",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "updatedby",
                table: "usersfriends",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "createdby",
                table: "users",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "deletedby",
                table: "users",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "updatedby",
                table: "users",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "createdby",
                table: "messages",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "deletedby",
                table: "messages",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "updatedby",
                table: "messages",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "createdby",
                table: "channelusers",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "deletedby",
                table: "channelusers",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "updatedby",
                table: "channelusers",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "createdby",
                table: "channels",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "deletedby",
                table: "channels",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "ispublic",
                table: "channels",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<Guid>(
                name: "updatedby",
                table: "channels",
                type: "uuid",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "createdby",
                table: "userspendingmessages");

            migrationBuilder.DropColumn(
                name: "deletedby",
                table: "userspendingmessages");

            migrationBuilder.DropColumn(
                name: "updatedby",
                table: "userspendingmessages");

            migrationBuilder.DropColumn(
                name: "createdby",
                table: "usersfriends");

            migrationBuilder.DropColumn(
                name: "deletedby",
                table: "usersfriends");

            migrationBuilder.DropColumn(
                name: "updatedby",
                table: "usersfriends");

            migrationBuilder.DropColumn(
                name: "createdby",
                table: "users");

            migrationBuilder.DropColumn(
                name: "deletedby",
                table: "users");

            migrationBuilder.DropColumn(
                name: "updatedby",
                table: "users");

            migrationBuilder.DropColumn(
                name: "createdby",
                table: "messages");

            migrationBuilder.DropColumn(
                name: "deletedby",
                table: "messages");

            migrationBuilder.DropColumn(
                name: "updatedby",
                table: "messages");

            migrationBuilder.DropColumn(
                name: "createdby",
                table: "channelusers");

            migrationBuilder.DropColumn(
                name: "deletedby",
                table: "channelusers");

            migrationBuilder.DropColumn(
                name: "updatedby",
                table: "channelusers");

            migrationBuilder.DropColumn(
                name: "createdby",
                table: "channels");

            migrationBuilder.DropColumn(
                name: "deletedby",
                table: "channels");

            migrationBuilder.DropColumn(
                name: "ispublic",
                table: "channels");

            migrationBuilder.DropColumn(
                name: "updatedby",
                table: "channels");
        }
    }
}
