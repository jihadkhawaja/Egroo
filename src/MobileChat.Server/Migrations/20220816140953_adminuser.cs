using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MobileChat.Server.Migrations
{
    public partial class adminuser : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsAdmin",
                table: "ChannelUsers",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsAdmin",
                table: "ChannelUsers");
        }
    }
}
