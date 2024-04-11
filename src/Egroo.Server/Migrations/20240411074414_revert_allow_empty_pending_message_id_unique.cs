using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Egroo.Server.Migrations
{
    /// <inheritdoc />
    public partial class revert_allow_empty_pending_message_id_unique : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_UsersPendingMessages_MessageId",
                table: "UsersPendingMessages");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_UsersPendingMessages_MessageId",
                table: "UsersPendingMessages",
                column: "MessageId",
                unique: true);
        }
    }
}
