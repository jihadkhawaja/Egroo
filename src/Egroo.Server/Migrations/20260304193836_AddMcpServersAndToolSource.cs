using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Egroo.Server.Migrations
{
    /// <inheritdoc />
    public partial class AddMcpServersAndToolSource : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "mcpserverid",
                table: "agenttools",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "source",
                table: "agenttools",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "agentmcpservers",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    agentdefinitionid = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    endpoint = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    apikey = table.Column<string>(type: "text", nullable: true),
                    isactive = table.Column<bool>(type: "boolean", nullable: false),
                    lastdiscoveredat = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    datecreated = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    dateupdated = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    datedeleted = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    createdby = table.Column<Guid>(type: "uuid", nullable: true),
                    updatedby = table.Column<Guid>(type: "uuid", nullable: true),
                    deletedby = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_agentmcpservers", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_agentmcpservers_agentdefinitionid",
                table: "agentmcpservers",
                column: "agentdefinitionid");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "agentmcpservers");

            migrationBuilder.DropColumn(
                name: "mcpserverid",
                table: "agenttools");

            migrationBuilder.DropColumn(
                name: "source",
                table: "agenttools");
        }
    }
}
