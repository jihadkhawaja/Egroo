using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Egroo.Server.Migrations
{
    /// <inheritdoc />
    public partial class agentchannele2ee : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "encryptionkeyid",
                table: "agentdefinitions",
                type: "character varying(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "encryptionkeyupdatedon",
                table: "agentdefinitions",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "encryptionprivatekey",
                table: "agentdefinitions",
                type: "character varying(12000)",
                maxLength: 12000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "encryptionpublickey",
                table: "agentdefinitions",
                type: "character varying(8000)",
                maxLength: 8000,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "agentpendingmessages",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    agentdefinitionid = table.Column<Guid>(type: "uuid", nullable: false),
                    messageid = table.Column<Guid>(type: "uuid", nullable: false),
                    content = table.Column<string>(type: "text", nullable: true),
                    datecreated = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    dateupdated = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    datedeleted = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    createdby = table.Column<Guid>(type: "uuid", nullable: true),
                    updatedby = table.Column<Guid>(type: "uuid", nullable: true),
                    deletedby = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_agentpendingmessages", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_agentpendingmessages_agentdefinitionid_messageid",
                table: "agentpendingmessages",
                columns: new[] { "agentdefinitionid", "messageid" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "agentpendingmessages");

            migrationBuilder.DropColumn(
                name: "encryptionkeyid",
                table: "agentdefinitions");

            migrationBuilder.DropColumn(
                name: "encryptionkeyupdatedon",
                table: "agentdefinitions");

            migrationBuilder.DropColumn(
                name: "encryptionprivatekey",
                table: "agentdefinitions");

            migrationBuilder.DropColumn(
                name: "encryptionpublickey",
                table: "agentdefinitions");
        }
    }
}
