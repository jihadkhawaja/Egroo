using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Egroo.Server.Migrations
{
    /// <inheritdoc />
    public partial class multideviceencryptionkeys : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "userencryptionkeys",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    userid = table.Column<Guid>(type: "uuid", nullable: false),
                    publickey = table.Column<string>(type: "text", nullable: false),
                    keyid = table.Column<string>(type: "text", nullable: false),
                    devicelabel = table.Column<string>(type: "text", nullable: true),
                    datecreated = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    dateupdated = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    datedeleted = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    createdby = table.Column<Guid>(type: "uuid", nullable: true),
                    updatedby = table.Column<Guid>(type: "uuid", nullable: true),
                    deletedby = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_userencryptionkeys", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_userencryptionkeys_userid",
                table: "userencryptionkeys",
                column: "userid");

            migrationBuilder.CreateIndex(
                name: "IX_userencryptionkeys_userid_keyid",
                table: "userencryptionkeys",
                columns: new[] { "userid", "keyid" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "userencryptionkeys");
        }
    }
}
