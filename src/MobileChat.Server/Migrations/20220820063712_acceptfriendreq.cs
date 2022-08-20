using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MobileChat.Server.Migrations
{
    public partial class acceptfriendreq : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsAccepted",
                table: "UsersFriends",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsAccepted",
                table: "UsersFriends");
        }
    }
}
