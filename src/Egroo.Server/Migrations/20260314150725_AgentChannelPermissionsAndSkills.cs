using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Egroo.Server.Migrations
{
    /// <inheritdoc />
    public partial class AgentChannelPermissionsAndSkills : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "addpermission",
                table: "agentdefinitions",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "skillsinstructionprompt",
                table: "agentdefinitions",
                type: "character varying(4000)",
                maxLength: 4000,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "agentskilldirectories",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    agentdefinitionid = table.Column<Guid>(type: "uuid", nullable: false),
                    path = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    isenabled = table.Column<bool>(type: "boolean", nullable: false),
                    datecreated = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    dateupdated = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    datedeleted = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    createdby = table.Column<Guid>(type: "uuid", nullable: true),
                    updatedby = table.Column<Guid>(type: "uuid", nullable: true),
                    deletedby = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_agentskilldirectories", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_agentskilldirectories_agentdefinitionid",
                table: "agentskilldirectories",
                column: "agentdefinitionid");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "agentskilldirectories");

            migrationBuilder.DropColumn(
                name: "addpermission",
                table: "agentdefinitions");

            migrationBuilder.DropColumn(
                name: "skillsinstructionprompt",
                table: "agentdefinitions");
        }
    }
}
