using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Egroo.Server.Migrations
{
    /// <inheritdoc />
    public partial class AddAgentTasks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "agenttasks",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    ownerid = table.Column<Guid>(type: "uuid", nullable: false),
                    agentid = table.Column<Guid>(type: "uuid", nullable: false),
                    requestid = table.Column<Guid>(type: "uuid", nullable: false),
                    prompt = table.Column<string>(type: "character varying(8192)", maxLength: 8192, nullable: false),
                    status = table.Column<int>(type: "integer", nullable: false),
                    createdat = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    startedat = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    finishedat = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    executiondeadline = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    result = table.Column<string>(type: "character varying(65536)", maxLength: 65536, nullable: true),
                    failure = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_agenttasks", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_agenttasks_ownerid_createdat_id",
                table: "agenttasks",
                columns: new[] { "ownerid", "createdat", "id" });

            migrationBuilder.CreateIndex(
                name: "IX_agenttasks_ownerid_requestid",
                table: "agenttasks",
                columns: new[] { "ownerid", "requestid" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_agenttasks_status_createdat_id",
                table: "agenttasks",
                columns: new[] { "status", "createdat", "id" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "agenttasks");
        }
    }
}
