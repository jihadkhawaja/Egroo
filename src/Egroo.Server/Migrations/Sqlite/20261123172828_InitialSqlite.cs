using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Egroo.Server.Migrations.Sqlite
{
    /// <inheritdoc />
    public partial class InitialSqlite : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "agentconversationmessages",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "TEXT", nullable: false),
                    agentconversationid = table.Column<Guid>(type: "TEXT", nullable: false),
                    role = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    content = table.Column<string>(type: "TEXT", nullable: false),
                    toolcallid = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    toolname = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    tokensused = table.Column<int>(type: "INTEGER", nullable: true),
                    datecreated = table.Column<long>(type: "INTEGER", nullable: false),
                    dateupdated = table.Column<long>(type: "INTEGER", nullable: true),
                    datedeleted = table.Column<long>(type: "INTEGER", nullable: true),
                    createdby = table.Column<Guid>(type: "TEXT", nullable: true),
                    updatedby = table.Column<Guid>(type: "TEXT", nullable: true),
                    deletedby = table.Column<Guid>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_agentconversationmessages", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "agentconversations",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "TEXT", nullable: false),
                    agentdefinitionid = table.Column<Guid>(type: "TEXT", nullable: false),
                    userid = table.Column<Guid>(type: "TEXT", nullable: false),
                    title = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    sessionstate = table.Column<string>(type: "TEXT", nullable: true),
                    datecreated = table.Column<long>(type: "INTEGER", nullable: false),
                    dateupdated = table.Column<long>(type: "INTEGER", nullable: true),
                    datedeleted = table.Column<long>(type: "INTEGER", nullable: true),
                    createdby = table.Column<Guid>(type: "TEXT", nullable: true),
                    updatedby = table.Column<Guid>(type: "TEXT", nullable: true),
                    deletedby = table.Column<Guid>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_agentconversations", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "agentdefinitions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "TEXT", nullable: false),
                    userid = table.Column<Guid>(type: "TEXT", nullable: false),
                    name = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    description = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: true),
                    instructions = table.Column<string>(type: "TEXT", nullable: true),
                    provider = table.Column<int>(type: "INTEGER", nullable: false),
                    model = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    apikey = table.Column<string>(type: "TEXT", nullable: true),
                    encryptionpublickey = table.Column<string>(type: "TEXT", maxLength: 8000, nullable: true),
                    encryptionprivatekey = table.Column<string>(type: "TEXT", maxLength: 12000, nullable: true),
                    encryptionkeyid = table.Column<string>(type: "TEXT", maxLength: 128, nullable: true),
                    encryptionkeyupdatedon = table.Column<long>(type: "INTEGER", nullable: true),
                    endpoint = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    isactive = table.Column<bool>(type: "INTEGER", nullable: false),
                    ispublished = table.Column<bool>(type: "INTEGER", nullable: false),
                    addpermission = table.Column<int>(type: "INTEGER", nullable: false),
                    temperature = table.Column<float>(type: "REAL", nullable: true),
                    maxtokens = table.Column<int>(type: "INTEGER", nullable: true),
                    skillsinstructionprompt = table.Column<string>(type: "TEXT", maxLength: 4000, nullable: true),
                    datecreated = table.Column<long>(type: "INTEGER", nullable: false),
                    dateupdated = table.Column<long>(type: "INTEGER", nullable: true),
                    datedeleted = table.Column<long>(type: "INTEGER", nullable: true),
                    createdby = table.Column<Guid>(type: "TEXT", nullable: true),
                    updatedby = table.Column<Guid>(type: "TEXT", nullable: true),
                    deletedby = table.Column<Guid>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_agentdefinitions", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "agentknowledgeitems",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "TEXT", nullable: false),
                    agentdefinitionid = table.Column<Guid>(type: "TEXT", nullable: false),
                    title = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    content = table.Column<string>(type: "TEXT", nullable: false),
                    isenabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    datecreated = table.Column<long>(type: "INTEGER", nullable: false),
                    dateupdated = table.Column<long>(type: "INTEGER", nullable: true),
                    datedeleted = table.Column<long>(type: "INTEGER", nullable: true),
                    createdby = table.Column<Guid>(type: "TEXT", nullable: true),
                    updatedby = table.Column<Guid>(type: "TEXT", nullable: true),
                    deletedby = table.Column<Guid>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_agentknowledgeitems", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "agentmcpservers",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "TEXT", nullable: false),
                    agentdefinitionid = table.Column<Guid>(type: "TEXT", nullable: false),
                    name = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    endpoint = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: false),
                    apikey = table.Column<string>(type: "TEXT", nullable: true),
                    isactive = table.Column<bool>(type: "INTEGER", nullable: false),
                    lastdiscoveredat = table.Column<long>(type: "INTEGER", nullable: true),
                    datecreated = table.Column<long>(type: "INTEGER", nullable: false),
                    dateupdated = table.Column<long>(type: "INTEGER", nullable: true),
                    datedeleted = table.Column<long>(type: "INTEGER", nullable: true),
                    createdby = table.Column<Guid>(type: "TEXT", nullable: true),
                    updatedby = table.Column<Guid>(type: "TEXT", nullable: true),
                    deletedby = table.Column<Guid>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_agentmcpservers", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "agentpendingmessages",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "TEXT", nullable: false),
                    agentdefinitionid = table.Column<Guid>(type: "TEXT", nullable: false),
                    messageid = table.Column<Guid>(type: "TEXT", nullable: false),
                    content = table.Column<string>(type: "TEXT", nullable: true),
                    datecreated = table.Column<long>(type: "INTEGER", nullable: false),
                    dateupdated = table.Column<long>(type: "INTEGER", nullable: true),
                    datedeleted = table.Column<long>(type: "INTEGER", nullable: true),
                    createdby = table.Column<Guid>(type: "TEXT", nullable: true),
                    updatedby = table.Column<Guid>(type: "TEXT", nullable: true),
                    deletedby = table.Column<Guid>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_agentpendingmessages", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "agentskilldirectories",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "TEXT", nullable: false),
                    agentdefinitionid = table.Column<Guid>(type: "TEXT", nullable: false),
                    name = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    path = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: false),
                    isenabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    datecreated = table.Column<long>(type: "INTEGER", nullable: false),
                    dateupdated = table.Column<long>(type: "INTEGER", nullable: true),
                    datedeleted = table.Column<long>(type: "INTEGER", nullable: true),
                    createdby = table.Column<Guid>(type: "TEXT", nullable: true),
                    updatedby = table.Column<Guid>(type: "TEXT", nullable: true),
                    deletedby = table.Column<Guid>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_agentskilldirectories", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "agenttools",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "TEXT", nullable: false),
                    agentdefinitionid = table.Column<Guid>(type: "TEXT", nullable: false),
                    name = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    description = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: false),
                    parametersschema = table.Column<string>(type: "TEXT", nullable: true),
                    isenabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    source = table.Column<int>(type: "INTEGER", nullable: false),
                    mcpserverid = table.Column<Guid>(type: "TEXT", nullable: true),
                    datecreated = table.Column<long>(type: "INTEGER", nullable: false),
                    dateupdated = table.Column<long>(type: "INTEGER", nullable: true),
                    datedeleted = table.Column<long>(type: "INTEGER", nullable: true),
                    createdby = table.Column<Guid>(type: "TEXT", nullable: true),
                    updatedby = table.Column<Guid>(type: "TEXT", nullable: true),
                    deletedby = table.Column<Guid>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_agenttools", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "channelagents",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "TEXT", nullable: false),
                    channelid = table.Column<Guid>(type: "TEXT", nullable: false),
                    agentdefinitionid = table.Column<Guid>(type: "TEXT", nullable: false),
                    addedbyuserid = table.Column<Guid>(type: "TEXT", nullable: false),
                    datecreated = table.Column<long>(type: "INTEGER", nullable: false),
                    dateupdated = table.Column<long>(type: "INTEGER", nullable: true),
                    datedeleted = table.Column<long>(type: "INTEGER", nullable: true),
                    createdby = table.Column<Guid>(type: "TEXT", nullable: true),
                    updatedby = table.Column<Guid>(type: "TEXT", nullable: true),
                    deletedby = table.Column<Guid>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_channelagents", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "channels",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "TEXT", nullable: false),
                    title = table.Column<string>(type: "TEXT", nullable: true),
                    ispublic = table.Column<bool>(type: "INTEGER", nullable: false),
                    datecreated = table.Column<long>(type: "INTEGER", nullable: false),
                    dateupdated = table.Column<long>(type: "INTEGER", nullable: true),
                    datedeleted = table.Column<long>(type: "INTEGER", nullable: true),
                    createdby = table.Column<Guid>(type: "TEXT", nullable: true),
                    updatedby = table.Column<Guid>(type: "TEXT", nullable: true),
                    deletedby = table.Column<Guid>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_channels", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "channelusers",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "TEXT", nullable: false),
                    channelid = table.Column<Guid>(type: "TEXT", nullable: false),
                    userid = table.Column<Guid>(type: "TEXT", nullable: false),
                    isadmin = table.Column<bool>(type: "INTEGER", nullable: false),
                    datecreated = table.Column<long>(type: "INTEGER", nullable: false),
                    dateupdated = table.Column<long>(type: "INTEGER", nullable: true),
                    datedeleted = table.Column<long>(type: "INTEGER", nullable: true),
                    createdby = table.Column<Guid>(type: "TEXT", nullable: true),
                    updatedby = table.Column<Guid>(type: "TEXT", nullable: true),
                    deletedby = table.Column<Guid>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_channelusers", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "messages",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "TEXT", nullable: false),
                    senderid = table.Column<Guid>(type: "TEXT", nullable: false),
                    channelid = table.Column<Guid>(type: "TEXT", nullable: false),
                    referenceid = table.Column<Guid>(type: "TEXT", nullable: false),
                    datesent = table.Column<long>(type: "INTEGER", nullable: true),
                    dateseen = table.Column<long>(type: "INTEGER", nullable: true),
                    agentdefinitionid = table.Column<Guid>(type: "TEXT", nullable: true),
                    datecreated = table.Column<long>(type: "INTEGER", nullable: false),
                    dateupdated = table.Column<long>(type: "INTEGER", nullable: true),
                    datedeleted = table.Column<long>(type: "INTEGER", nullable: true),
                    createdby = table.Column<Guid>(type: "TEXT", nullable: true),
                    updatedby = table.Column<Guid>(type: "TEXT", nullable: true),
                    deletedby = table.Column<Guid>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_messages", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "useragentfriends",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "TEXT", nullable: false),
                    userid = table.Column<Guid>(type: "TEXT", nullable: false),
                    agentdefinitionid = table.Column<Guid>(type: "TEXT", nullable: false),
                    datecreated = table.Column<long>(type: "INTEGER", nullable: false),
                    dateupdated = table.Column<long>(type: "INTEGER", nullable: true),
                    datedeleted = table.Column<long>(type: "INTEGER", nullable: true),
                    createdby = table.Column<Guid>(type: "TEXT", nullable: true),
                    updatedby = table.Column<Guid>(type: "TEXT", nullable: true),
                    deletedby = table.Column<Guid>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_useragentfriends", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "userencryptionkeys",
                columns: table => new
                {
                    id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    userid = table.Column<Guid>(type: "TEXT", nullable: false),
                    publickey = table.Column<string>(type: "TEXT", nullable: false),
                    keyid = table.Column<string>(type: "TEXT", nullable: false),
                    devicelabel = table.Column<string>(type: "TEXT", nullable: true),
                    datecreated = table.Column<long>(type: "INTEGER", nullable: false),
                    dateupdated = table.Column<long>(type: "INTEGER", nullable: true),
                    datedeleted = table.Column<long>(type: "INTEGER", nullable: true),
                    createdby = table.Column<Guid>(type: "TEXT", nullable: true),
                    updatedby = table.Column<Guid>(type: "TEXT", nullable: true),
                    deletedby = table.Column<Guid>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_userencryptionkeys", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "users",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "TEXT", nullable: false),
                    password = table.Column<string>(type: "TEXT", nullable: true),
                    datecreated = table.Column<long>(type: "INTEGER", nullable: false),
                    dateupdated = table.Column<long>(type: "INTEGER", nullable: true),
                    datedeleted = table.Column<long>(type: "INTEGER", nullable: true),
                    createdby = table.Column<Guid>(type: "TEXT", nullable: true),
                    updatedby = table.Column<Guid>(type: "TEXT", nullable: true),
                    deletedby = table.Column<Guid>(type: "TEXT", nullable: true),
                    username = table.Column<string>(type: "TEXT", nullable: false),
                    role = table.Column<string>(type: "TEXT", nullable: false),
                    lastlogindate = table.Column<long>(type: "INTEGER", nullable: true),
                    encryptionpublickey = table.Column<string>(type: "TEXT", nullable: true),
                    encryptionkeyid = table.Column<string>(type: "TEXT", nullable: true),
                    encryptionkeyupdatedon = table.Column<long>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_users", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "usersfriends",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "TEXT", nullable: false),
                    userid = table.Column<Guid>(type: "TEXT", nullable: false),
                    frienduserid = table.Column<Guid>(type: "TEXT", nullable: false),
                    dateacceptedon = table.Column<long>(type: "INTEGER", nullable: true),
                    datecreated = table.Column<long>(type: "INTEGER", nullable: false),
                    dateupdated = table.Column<long>(type: "INTEGER", nullable: true),
                    datedeleted = table.Column<long>(type: "INTEGER", nullable: true),
                    createdby = table.Column<Guid>(type: "TEXT", nullable: true),
                    updatedby = table.Column<Guid>(type: "TEXT", nullable: true),
                    deletedby = table.Column<Guid>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_usersfriends", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "userspendingmessages",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "TEXT", nullable: false),
                    userid = table.Column<Guid>(type: "TEXT", nullable: false),
                    messageid = table.Column<Guid>(type: "TEXT", nullable: false),
                    content = table.Column<string>(type: "TEXT", nullable: true),
                    dateuserreceivedon = table.Column<long>(type: "INTEGER", nullable: true),
                    datecreated = table.Column<long>(type: "INTEGER", nullable: false),
                    dateupdated = table.Column<long>(type: "INTEGER", nullable: true),
                    datedeleted = table.Column<long>(type: "INTEGER", nullable: true),
                    createdby = table.Column<Guid>(type: "TEXT", nullable: true),
                    updatedby = table.Column<Guid>(type: "TEXT", nullable: true),
                    deletedby = table.Column<Guid>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_userspendingmessages", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "userdetail",
                columns: table => new
                {
                    userid = table.Column<Guid>(type: "TEXT", nullable: false),
                    firstname = table.Column<string>(type: "TEXT", nullable: true),
                    lastname = table.Column<string>(type: "TEXT", nullable: true),
                    displayname = table.Column<string>(type: "TEXT", nullable: true),
                    email = table.Column<string>(type: "TEXT", nullable: true),
                    phonenumber = table.Column<string>(type: "TEXT", nullable: true),
                    phonecountrycode = table.Column<string>(type: "TEXT", nullable: true),
                    region = table.Column<string>(type: "TEXT", nullable: true),
                    country = table.Column<string>(type: "TEXT", nullable: true),
                    sex = table.Column<int>(type: "INTEGER", nullable: false),
                    pronounce = table.Column<string>(type: "TEXT", nullable: true),
                    interests = table.Column<string>(type: "TEXT", nullable: true),
                    shortdescription = table.Column<string>(type: "TEXT", nullable: true),
                    fulldescription = table.Column<string>(type: "TEXT", nullable: true),
                    sociallinks = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_userdetail", x => x.userid);
                    table.ForeignKey(
                        name: "FK_userdetail_users_userid",
                        column: x => x.userid,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "userfeedback",
                columns: table => new
                {
                    id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    userid = table.Column<Guid>(type: "TEXT", nullable: false),
                    text = table.Column<string>(type: "TEXT", nullable: false),
                    datecreated = table.Column<long>(type: "INTEGER", nullable: false),
                    dateupdated = table.Column<long>(type: "INTEGER", nullable: true),
                    datedeleted = table.Column<long>(type: "INTEGER", nullable: true),
                    createdby = table.Column<Guid>(type: "TEXT", nullable: true),
                    updatedby = table.Column<Guid>(type: "TEXT", nullable: true),
                    deletedby = table.Column<Guid>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_userfeedback", x => x.id);
                    table.ForeignKey(
                        name: "FK_userfeedback_users_userid",
                        column: x => x.userid,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "usersecurity",
                columns: table => new
                {
                    userid = table.Column<Guid>(type: "TEXT", nullable: false),
                    istwofactorenabled = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_usersecurity", x => x.userid);
                    table.ForeignKey(
                        name: "FK_usersecurity_users_userid",
                        column: x => x.userid,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "userstorage",
                columns: table => new
                {
                    userid = table.Column<Guid>(type: "TEXT", nullable: false),
                    avatarimagebase64 = table.Column<string>(type: "TEXT", nullable: true),
                    avatarcontenttype = table.Column<string>(type: "TEXT", nullable: true),
                    coverimagebase64 = table.Column<string>(type: "TEXT", nullable: true),
                    covercontenttype = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_userstorage", x => x.userid);
                    table.ForeignKey(
                        name: "FK_userstorage_users_userid",
                        column: x => x.userid,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
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
                name: "IX_agentmcpservers_agentdefinitionid",
                table: "agentmcpservers",
                column: "agentdefinitionid");

            migrationBuilder.CreateIndex(
                name: "IX_agentpendingmessages_agentdefinitionid_messageid",
                table: "agentpendingmessages",
                columns: new[] { "agentdefinitionid", "messageid" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_agentskilldirectories_agentdefinitionid",
                table: "agentskilldirectories",
                column: "agentdefinitionid");

            migrationBuilder.CreateIndex(
                name: "IX_agenttools_agentdefinitionid",
                table: "agenttools",
                column: "agentdefinitionid");

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

            migrationBuilder.CreateIndex(
                name: "IX_userencryptionkeys_userid",
                table: "userencryptionkeys",
                column: "userid");

            migrationBuilder.CreateIndex(
                name: "IX_userencryptionkeys_userid_keyid",
                table: "userencryptionkeys",
                columns: new[] { "userid", "keyid" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_userfeedback_userid",
                table: "userfeedback",
                column: "userid");

            migrationBuilder.CreateIndex(
                name: "IX_users_username",
                table: "users",
                column: "username",
                unique: true);
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
                name: "agentmcpservers");

            migrationBuilder.DropTable(
                name: "agentpendingmessages");

            migrationBuilder.DropTable(
                name: "agentskilldirectories");

            migrationBuilder.DropTable(
                name: "agenttools");

            migrationBuilder.DropTable(
                name: "channelagents");

            migrationBuilder.DropTable(
                name: "channels");

            migrationBuilder.DropTable(
                name: "channelusers");

            migrationBuilder.DropTable(
                name: "messages");

            migrationBuilder.DropTable(
                name: "useragentfriends");

            migrationBuilder.DropTable(
                name: "userdetail");

            migrationBuilder.DropTable(
                name: "userencryptionkeys");

            migrationBuilder.DropTable(
                name: "userfeedback");

            migrationBuilder.DropTable(
                name: "usersecurity");

            migrationBuilder.DropTable(
                name: "usersfriends");

            migrationBuilder.DropTable(
                name: "userspendingmessages");

            migrationBuilder.DropTable(
                name: "userstorage");

            migrationBuilder.DropTable(
                name: "users");
        }
    }
}
