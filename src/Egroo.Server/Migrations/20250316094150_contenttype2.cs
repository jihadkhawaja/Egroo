using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Egroo.Server.Migrations
{
    /// <inheritdoc />
    public partial class contenttype2 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "contenttype",
                table: "userstorage",
                newName: "covercontenttype");

            migrationBuilder.AddColumn<string>(
                name: "avatarcontenttype",
                table: "userstorage",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "avatarcontenttype",
                table: "userstorage");

            migrationBuilder.RenameColumn(
                name: "covercontenttype",
                table: "userstorage",
                newName: "contenttype");
        }
    }
}
