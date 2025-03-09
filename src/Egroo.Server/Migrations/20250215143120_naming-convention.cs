using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Egroo.Server.Migrations
{
    /// <inheritdoc />
    public partial class namingconvention : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropPrimaryKey(
                name: "PK_UsersPendingMessages",
                table: "UsersPendingMessages");

            migrationBuilder.DropPrimaryKey(
                name: "PK_UsersFriends",
                table: "UsersFriends");

            migrationBuilder.DropPrimaryKey(
                name: "PK_Users",
                table: "Users");

            migrationBuilder.DropPrimaryKey(
                name: "PK_Messages",
                table: "Messages");

            migrationBuilder.DropPrimaryKey(
                name: "PK_ChannelUsers",
                table: "ChannelUsers");

            migrationBuilder.DropPrimaryKey(
                name: "PK_Channels",
                table: "Channels");

            migrationBuilder.RenameTable(
                name: "UsersPendingMessages",
                newName: "userspendingmessages");

            migrationBuilder.RenameTable(
                name: "UsersFriends",
                newName: "usersfriends");

            migrationBuilder.RenameTable(
                name: "Users",
                newName: "users");

            migrationBuilder.RenameTable(
                name: "Messages",
                newName: "messages");

            migrationBuilder.RenameTable(
                name: "ChannelUsers",
                newName: "channelusers");

            migrationBuilder.RenameTable(
                name: "Channels",
                newName: "channels");

            migrationBuilder.RenameColumn(
                name: "UserId",
                table: "userspendingmessages",
                newName: "userid");

            migrationBuilder.RenameColumn(
                name: "MessageId",
                table: "userspendingmessages",
                newName: "messageid");

            migrationBuilder.RenameColumn(
                name: "DateUserReceivedOn",
                table: "userspendingmessages",
                newName: "dateuserreceivedon");

            migrationBuilder.RenameColumn(
                name: "DateUpdated",
                table: "userspendingmessages",
                newName: "dateupdated");

            migrationBuilder.RenameColumn(
                name: "DateDeleted",
                table: "userspendingmessages",
                newName: "datedeleted");

            migrationBuilder.RenameColumn(
                name: "DateCreated",
                table: "userspendingmessages",
                newName: "datecreated");

            migrationBuilder.RenameColumn(
                name: "Content",
                table: "userspendingmessages",
                newName: "content");

            migrationBuilder.RenameColumn(
                name: "Id",
                table: "userspendingmessages",
                newName: "id");

            migrationBuilder.RenameColumn(
                name: "UserId",
                table: "usersfriends",
                newName: "userid");

            migrationBuilder.RenameColumn(
                name: "FriendUserId",
                table: "usersfriends",
                newName: "frienduserid");

            migrationBuilder.RenameColumn(
                name: "DateUpdated",
                table: "usersfriends",
                newName: "dateupdated");

            migrationBuilder.RenameColumn(
                name: "DateDeleted",
                table: "usersfriends",
                newName: "datedeleted");

            migrationBuilder.RenameColumn(
                name: "DateCreated",
                table: "usersfriends",
                newName: "datecreated");

            migrationBuilder.RenameColumn(
                name: "DateAcceptedOn",
                table: "usersfriends",
                newName: "dateacceptedon");

            migrationBuilder.RenameColumn(
                name: "Id",
                table: "usersfriends",
                newName: "id");

            migrationBuilder.RenameColumn(
                name: "Username",
                table: "users",
                newName: "username");

            migrationBuilder.RenameColumn(
                name: "Role",
                table: "users",
                newName: "role");

            migrationBuilder.RenameColumn(
                name: "Password",
                table: "users",
                newName: "password");

            migrationBuilder.RenameColumn(
                name: "LastLoginDate",
                table: "users",
                newName: "lastlogindate");

            migrationBuilder.RenameColumn(
                name: "IsOnline",
                table: "users",
                newName: "isonline");

            migrationBuilder.RenameColumn(
                name: "InCall",
                table: "users",
                newName: "incall");

            migrationBuilder.RenameColumn(
                name: "DateUpdated",
                table: "users",
                newName: "dateupdated");

            migrationBuilder.RenameColumn(
                name: "DateDeleted",
                table: "users",
                newName: "datedeleted");

            migrationBuilder.RenameColumn(
                name: "DateCreated",
                table: "users",
                newName: "datecreated");

            migrationBuilder.RenameColumn(
                name: "ConnectionId",
                table: "users",
                newName: "connectionid");

            migrationBuilder.RenameColumn(
                name: "AvatarBase64",
                table: "users",
                newName: "avatarbase64");

            migrationBuilder.RenameColumn(
                name: "Id",
                table: "users",
                newName: "id");

            migrationBuilder.RenameIndex(
                name: "IX_Users_Username",
                table: "users",
                newName: "IX_users_username");

            migrationBuilder.RenameColumn(
                name: "SenderId",
                table: "messages",
                newName: "senderid");

            migrationBuilder.RenameColumn(
                name: "ReferenceId",
                table: "messages",
                newName: "referenceid");

            migrationBuilder.RenameColumn(
                name: "DateUpdated",
                table: "messages",
                newName: "dateupdated");

            migrationBuilder.RenameColumn(
                name: "DateSent",
                table: "messages",
                newName: "datesent");

            migrationBuilder.RenameColumn(
                name: "DateSeen",
                table: "messages",
                newName: "dateseen");

            migrationBuilder.RenameColumn(
                name: "DateDeleted",
                table: "messages",
                newName: "datedeleted");

            migrationBuilder.RenameColumn(
                name: "DateCreated",
                table: "messages",
                newName: "datecreated");

            migrationBuilder.RenameColumn(
                name: "ChannelId",
                table: "messages",
                newName: "channelid");

            migrationBuilder.RenameColumn(
                name: "Id",
                table: "messages",
                newName: "id");

            migrationBuilder.RenameColumn(
                name: "UserId",
                table: "channelusers",
                newName: "userid");

            migrationBuilder.RenameColumn(
                name: "IsAdmin",
                table: "channelusers",
                newName: "isadmin");

            migrationBuilder.RenameColumn(
                name: "DateUpdated",
                table: "channelusers",
                newName: "dateupdated");

            migrationBuilder.RenameColumn(
                name: "DateDeleted",
                table: "channelusers",
                newName: "datedeleted");

            migrationBuilder.RenameColumn(
                name: "DateCreated",
                table: "channelusers",
                newName: "datecreated");

            migrationBuilder.RenameColumn(
                name: "ChannelId",
                table: "channelusers",
                newName: "channelid");

            migrationBuilder.RenameColumn(
                name: "Id",
                table: "channelusers",
                newName: "id");

            migrationBuilder.RenameColumn(
                name: "DateUpdated",
                table: "channels",
                newName: "dateupdated");

            migrationBuilder.RenameColumn(
                name: "DateDeleted",
                table: "channels",
                newName: "datedeleted");

            migrationBuilder.RenameColumn(
                name: "DateCreated",
                table: "channels",
                newName: "datecreated");

            migrationBuilder.RenameColumn(
                name: "Id",
                table: "channels",
                newName: "id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_userspendingmessages",
                table: "userspendingmessages",
                column: "id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_usersfriends",
                table: "usersfriends",
                column: "id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_users",
                table: "users",
                column: "id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_messages",
                table: "messages",
                column: "id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_channelusers",
                table: "channelusers",
                column: "id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_channels",
                table: "channels",
                column: "id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropPrimaryKey(
                name: "PK_userspendingmessages",
                table: "userspendingmessages");

            migrationBuilder.DropPrimaryKey(
                name: "PK_usersfriends",
                table: "usersfriends");

            migrationBuilder.DropPrimaryKey(
                name: "PK_users",
                table: "users");

            migrationBuilder.DropPrimaryKey(
                name: "PK_messages",
                table: "messages");

            migrationBuilder.DropPrimaryKey(
                name: "PK_channelusers",
                table: "channelusers");

            migrationBuilder.DropPrimaryKey(
                name: "PK_channels",
                table: "channels");

            migrationBuilder.RenameTable(
                name: "userspendingmessages",
                newName: "UsersPendingMessages");

            migrationBuilder.RenameTable(
                name: "usersfriends",
                newName: "UsersFriends");

            migrationBuilder.RenameTable(
                name: "users",
                newName: "Users");

            migrationBuilder.RenameTable(
                name: "messages",
                newName: "Messages");

            migrationBuilder.RenameTable(
                name: "channelusers",
                newName: "ChannelUsers");

            migrationBuilder.RenameTable(
                name: "channels",
                newName: "Channels");

            migrationBuilder.RenameColumn(
                name: "userid",
                table: "UsersPendingMessages",
                newName: "UserId");

            migrationBuilder.RenameColumn(
                name: "messageid",
                table: "UsersPendingMessages",
                newName: "MessageId");

            migrationBuilder.RenameColumn(
                name: "dateuserreceivedon",
                table: "UsersPendingMessages",
                newName: "DateUserReceivedOn");

            migrationBuilder.RenameColumn(
                name: "dateupdated",
                table: "UsersPendingMessages",
                newName: "DateUpdated");

            migrationBuilder.RenameColumn(
                name: "datedeleted",
                table: "UsersPendingMessages",
                newName: "DateDeleted");

            migrationBuilder.RenameColumn(
                name: "datecreated",
                table: "UsersPendingMessages",
                newName: "DateCreated");

            migrationBuilder.RenameColumn(
                name: "content",
                table: "UsersPendingMessages",
                newName: "Content");

            migrationBuilder.RenameColumn(
                name: "id",
                table: "UsersPendingMessages",
                newName: "Id");

            migrationBuilder.RenameColumn(
                name: "userid",
                table: "UsersFriends",
                newName: "UserId");

            migrationBuilder.RenameColumn(
                name: "frienduserid",
                table: "UsersFriends",
                newName: "FriendUserId");

            migrationBuilder.RenameColumn(
                name: "dateupdated",
                table: "UsersFriends",
                newName: "DateUpdated");

            migrationBuilder.RenameColumn(
                name: "datedeleted",
                table: "UsersFriends",
                newName: "DateDeleted");

            migrationBuilder.RenameColumn(
                name: "datecreated",
                table: "UsersFriends",
                newName: "DateCreated");

            migrationBuilder.RenameColumn(
                name: "dateacceptedon",
                table: "UsersFriends",
                newName: "DateAcceptedOn");

            migrationBuilder.RenameColumn(
                name: "id",
                table: "UsersFriends",
                newName: "Id");

            migrationBuilder.RenameColumn(
                name: "username",
                table: "Users",
                newName: "Username");

            migrationBuilder.RenameColumn(
                name: "role",
                table: "Users",
                newName: "Role");

            migrationBuilder.RenameColumn(
                name: "password",
                table: "Users",
                newName: "Password");

            migrationBuilder.RenameColumn(
                name: "lastlogindate",
                table: "Users",
                newName: "LastLoginDate");

            migrationBuilder.RenameColumn(
                name: "isonline",
                table: "Users",
                newName: "IsOnline");

            migrationBuilder.RenameColumn(
                name: "incall",
                table: "Users",
                newName: "InCall");

            migrationBuilder.RenameColumn(
                name: "dateupdated",
                table: "Users",
                newName: "DateUpdated");

            migrationBuilder.RenameColumn(
                name: "datedeleted",
                table: "Users",
                newName: "DateDeleted");

            migrationBuilder.RenameColumn(
                name: "datecreated",
                table: "Users",
                newName: "DateCreated");

            migrationBuilder.RenameColumn(
                name: "connectionid",
                table: "Users",
                newName: "ConnectionId");

            migrationBuilder.RenameColumn(
                name: "avatarbase64",
                table: "Users",
                newName: "AvatarBase64");

            migrationBuilder.RenameColumn(
                name: "id",
                table: "Users",
                newName: "Id");

            migrationBuilder.RenameIndex(
                name: "IX_users_username",
                table: "Users",
                newName: "IX_Users_Username");

            migrationBuilder.RenameColumn(
                name: "senderid",
                table: "Messages",
                newName: "SenderId");

            migrationBuilder.RenameColumn(
                name: "referenceid",
                table: "Messages",
                newName: "ReferenceId");

            migrationBuilder.RenameColumn(
                name: "dateupdated",
                table: "Messages",
                newName: "DateUpdated");

            migrationBuilder.RenameColumn(
                name: "datesent",
                table: "Messages",
                newName: "DateSent");

            migrationBuilder.RenameColumn(
                name: "dateseen",
                table: "Messages",
                newName: "DateSeen");

            migrationBuilder.RenameColumn(
                name: "datedeleted",
                table: "Messages",
                newName: "DateDeleted");

            migrationBuilder.RenameColumn(
                name: "datecreated",
                table: "Messages",
                newName: "DateCreated");

            migrationBuilder.RenameColumn(
                name: "channelid",
                table: "Messages",
                newName: "ChannelId");

            migrationBuilder.RenameColumn(
                name: "id",
                table: "Messages",
                newName: "Id");

            migrationBuilder.RenameColumn(
                name: "userid",
                table: "ChannelUsers",
                newName: "UserId");

            migrationBuilder.RenameColumn(
                name: "isadmin",
                table: "ChannelUsers",
                newName: "IsAdmin");

            migrationBuilder.RenameColumn(
                name: "dateupdated",
                table: "ChannelUsers",
                newName: "DateUpdated");

            migrationBuilder.RenameColumn(
                name: "datedeleted",
                table: "ChannelUsers",
                newName: "DateDeleted");

            migrationBuilder.RenameColumn(
                name: "datecreated",
                table: "ChannelUsers",
                newName: "DateCreated");

            migrationBuilder.RenameColumn(
                name: "channelid",
                table: "ChannelUsers",
                newName: "ChannelId");

            migrationBuilder.RenameColumn(
                name: "id",
                table: "ChannelUsers",
                newName: "Id");

            migrationBuilder.RenameColumn(
                name: "dateupdated",
                table: "Channels",
                newName: "DateUpdated");

            migrationBuilder.RenameColumn(
                name: "datedeleted",
                table: "Channels",
                newName: "DateDeleted");

            migrationBuilder.RenameColumn(
                name: "datecreated",
                table: "Channels",
                newName: "DateCreated");

            migrationBuilder.RenameColumn(
                name: "id",
                table: "Channels",
                newName: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_UsersPendingMessages",
                table: "UsersPendingMessages",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_UsersFriends",
                table: "UsersFriends",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_Users",
                table: "Users",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_Messages",
                table: "Messages",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_ChannelUsers",
                table: "ChannelUsers",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_Channels",
                table: "Channels",
                column: "Id");
        }
    }
}
