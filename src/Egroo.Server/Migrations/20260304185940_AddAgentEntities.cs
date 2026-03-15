using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Egroo.Server.Migrations
{
    /// <inheritdoc />
    public partial class AddAgentEntities : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "agentconversationmessages",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    agentconversationid = table.Column<Guid>(type: "uuid", nullable: false),
                    role = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    content = table.Column<string>(type: "text", nullable: false),
                    toolcallid = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    toolname = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    tokensused = table.Column<int>(type: "integer", nullable: true),
                    datecreated = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    dateupdated = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    datedeleted = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    createdby = table.Column<Guid>(type: "uuid", nullable: true),
                    updatedby = table.Column<Guid>(type: "uuid", nullable: true),
                    deletedby = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_agentconversationmessages", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "agentconversations",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    agentdefinitionid = table.Column<Guid>(type: "uuid", nullable: false),
                    userid = table.Column<Guid>(type: "uuid", nullable: false),
                    title = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    sessionstate = table.Column<string>(type: "text", nullable: true),
                    datecreated = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    dateupdated = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    datedeleted = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    createdby = table.Column<Guid>(type: "uuid", nullable: true),
                    updatedby = table.Column<Guid>(type: "uuid", nullable: true),
                    deletedby = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_agentconversations", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "agentdefinitions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    userid = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    instructions = table.Column<string>(type: "text", nullable: true),
                    provider = table.Column<int>(type: "integer", nullable: false),
                    model = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    apikey = table.Column<string>(type: "text", nullable: true),
                    endpoint = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    isactive = table.Column<bool>(type: "boolean", nullable: false),
                    temperature = table.Column<float>(type: "real", nullable: true),
                    maxtokens = table.Column<int>(type: "integer", nullable: true),
                    datecreated = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    dateupdated = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    datedeleted = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    createdby = table.Column<Guid>(type: "uuid", nullable: true),
                    updatedby = table.Column<Guid>(type: "uuid", nullable: true),
                    deletedby = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_agentdefinitions", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "agentknowledgeitems",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    agentdefinitionid = table.Column<Guid>(type: "uuid", nullable: false),
                    title = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    content = table.Column<string>(type: "text", nullable: false),
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
                    table.PrimaryKey("PK_agentknowledgeitems", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "agenttools",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    agentdefinitionid = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    parametersschema = table.Column<string>(type: "text", nullable: true),
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
                    table.PrimaryKey("PK_agenttools", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_agentconversationmessages_agentconversationid",
                table: "agentconversationmessages",
                column: "agentconversationid");

            migrationBuilder.CreateIndex(
                name: "IX_agentconversations_agentdefinitionid_userid",
                table: "agentconversations",
                columns: new[] { "agentdefinitionid", "userid" });

            migrationBuilder.CreateIndex(
                name: "IX_agentdefinitions_userid",
                table: "agentdefinitions",
                column: "userid");

            migrationBuilder.CreateIndex(
                name: "IX_agentknowledgeitems_agentdefinitionid",
                table: "agentknowledgeitems",
                column: "agentdefinitionid");

            migrationBuilder.CreateIndex(
                name: "IX_agenttools_agentdefinitionid",
                table: "agenttools",
                column: "agentdefinitionid");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "agentconversationmessages");

            migrationBuilder.DropTable(
                name: "agentconversations");

            migrationBuilder.DropTable(
                name: "agentdefinitions");

            migrationBuilder.DropTable(
                name: "agentknowledgeitems");

            migrationBuilder.DropTable(
                name: "agenttools");
        }
    }
}
