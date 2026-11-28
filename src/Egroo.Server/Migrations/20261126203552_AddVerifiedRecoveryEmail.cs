using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Egroo.Server.Migrations
{
    /// <inheritdoc />
    public partial class AddVerifiedRecoveryEmail : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "sessionrevision",
                table: "users",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.CreateTable(
                name: "userrecoverystates",
                columns: table => new
                {
                    userid = table.Column<Guid>(type: "uuid", nullable: false),
                    protectedaddress = table.Column<string>(type: "text", nullable: true),
                    protectedpendingaddress = table.Column<string>(type: "text", nullable: true),
                    confirmationhash = table.Column<string>(type: "text", nullable: true),
                    confirmationexpiresat = table.Column<long>(type: "bigint", nullable: true),
                    confirmationsentat = table.Column<long>(type: "bigint", nullable: true),
                    resethash = table.Column<string>(type: "text", nullable: true),
                    resetexpiresat = table.Column<long>(type: "bigint", nullable: true),
                    resetsentat = table.Column<long>(type: "bigint", nullable: true),
                    revision = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_userrecoverystates", x => x.userid);
                    table.ForeignKey(
                        name: "FK_userrecoverystates_users_userid",
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
                name: "userrecoverystates");

            migrationBuilder.DropColumn(
                name: "sessionrevision",
                table: "users");
        }
    }
}
