using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Egroo.Server.Migrations
{
    /// <inheritdoc />
    public partial class userdetails : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "avatarbase64",
                table: "users");

            migrationBuilder.CreateTable(
                name: "userdetail",
                columns: table => new
                {
                    userid = table.Column<Guid>(type: "uuid", nullable: false),
                    firstname = table.Column<string>(type: "text", nullable: true),
                    lastname = table.Column<string>(type: "text", nullable: true),
                    displayname = table.Column<string>(type: "text", nullable: true),
                    email = table.Column<string>(type: "text", nullable: true),
                    phonenumber = table.Column<string>(type: "text", nullable: true),
                    phonecountrycode = table.Column<string>(type: "text", nullable: true),
                    region = table.Column<string>(type: "text", nullable: true),
                    country = table.Column<string>(type: "text", nullable: true),
                    sex = table.Column<int>(type: "integer", nullable: false),
                    pronounce = table.Column<string>(type: "text", nullable: true),
                    interests = table.Column<string>(type: "text", nullable: true),
                    shortdescription = table.Column<string>(type: "text", nullable: true),
                    fulldescription = table.Column<string>(type: "text", nullable: true),
                    sociallinks = table.Column<List<string>>(type: "text[]", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_userdetail", x => x.userid);
                    table.ForeignKey(
                        name: "FK_userdetail_users_userid",
                        column: x => x.userid,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "usersecurity",
                columns: table => new
                {
                    userid = table.Column<Guid>(type: "uuid", nullable: false),
                    istwofactorenabled = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_usersecurity", x => x.userid);
                    table.ForeignKey(
                        name: "FK_usersecurity_users_userid",
                        column: x => x.userid,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "userstorage",
                columns: table => new
                {
                    userid = table.Column<Guid>(type: "uuid", nullable: false),
                    avatarimagebase64 = table.Column<string>(type: "text", nullable: true),
                    coverimagebase64 = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_userstorage", x => x.userid);
                    table.ForeignKey(
                        name: "FK_userstorage_users_userid",
                        column: x => x.userid,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "userdetail");

            migrationBuilder.DropTable(
                name: "usersecurity");

            migrationBuilder.DropTable(
                name: "userstorage");

            migrationBuilder.AddColumn<string>(
                name: "avatarbase64",
                table: "users",
                type: "text",
                nullable: true);
        }
    }
}
