using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Egroo.Server.Migrations
{
    /// <inheritdoc />
    public partial class AgentChannelSupport : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "agentdefinitionid",
                table: "messages",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "ispublished",
                table: "agentdefinitions",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "channelagents",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    channelid = table.Column<Guid>(type: "uuid", nullable: false),
                    agentdefinitionid = table.Column<Guid>(type: "uuid", nullable: false),
                    addedbyuserid = table.Column<Guid>(type: "uuid", nullable: false),
                    datecreated = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    dateupdated = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    datedeleted = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    createdby = table.Column<Guid>(type: "uuid", nullable: true),
                    updatedby = table.Column<Guid>(type: "uuid", nullable: true),
                    deletedby = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_channelagents", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "useragentfriends",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    userid = table.Column<Guid>(type: "uuid", nullable: false),
                    agentdefinitionid = table.Column<Guid>(type: "uuid", nullable: false),
                    datecreated = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    dateupdated = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    datedeleted = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    createdby = table.Column<Guid>(type: "uuid", nullable: true),
                    updatedby = table.Column<Guid>(type: "uuid", nullable: true),
                    deletedby = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_useragentfriends", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_channelagents_channelid_agentdefinitionid",
                table: "channelagents",
                columns: new[] { "channelid", "agentdefinitionid" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_useragentfriends_userid_agentdefinitionid",
                table: "useragentfriends",
                columns: new[] { "userid", "agentdefinitionid" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "channelagents");

            migrationBuilder.DropTable(
                name: "useragentfriends");

            migrationBuilder.DropColumn(
                name: "agentdefinitionid",
                table: "messages");

            migrationBuilder.DropColumn(
                name: "ispublished",
                table: "agentdefinitions");
        }
    }
}
