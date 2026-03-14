using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Egroo.Server.Migrations
{
    /// <inheritdoc />
    public partial class AgentSkillName : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "name",
                table: "agentskilldirectories",
                type: "character varying(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "name",
                table: "agentskilldirectories");
        }
    }
}
