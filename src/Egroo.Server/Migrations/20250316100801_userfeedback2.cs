using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Egroo.Server.Migrations
{
    /// <inheritdoc />
    public partial class userfeedback2 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_userfeedback_userid",
                table: "userfeedback");

            migrationBuilder.CreateIndex(
                name: "IX_userfeedback_userid",
                table: "userfeedback",
                column: "userid");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_userfeedback_userid",
                table: "userfeedback");

            migrationBuilder.CreateIndex(
                name: "IX_userfeedback_userid",
                table: "userfeedback",
                column: "userid",
                unique: true);
        }
    }
}
