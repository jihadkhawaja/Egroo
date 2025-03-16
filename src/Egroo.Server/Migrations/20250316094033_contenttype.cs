using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Egroo.Server.Migrations
{
    /// <inheritdoc />
    public partial class contenttype : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "contenttype",
                table: "userstorage",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "contenttype",
                table: "userstorage");
        }
    }
}
