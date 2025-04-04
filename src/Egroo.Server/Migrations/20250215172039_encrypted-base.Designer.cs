﻿// <auto-generated />
using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;
using jihadkhawaja.chat.server.Database;

#nullable disable

namespace Egroo.Server.Migrations
{
    [DbContext(typeof(DataContext))]
    [Migration("20250215172039_encrypted-base")]
    partial class encryptedbase
    {
        /// <inheritdoc />
        protected override void BuildTargetModel(ModelBuilder modelBuilder)
        {
#pragma warning disable 612, 618
            modelBuilder
                .HasAnnotation("ProductVersion", "8.0.3")
                .HasAnnotation("Relational:MaxIdentifierLength", 63);

            NpgsqlModelBuilderExtensions.UseIdentityByDefaultColumns(modelBuilder);

            modelBuilder.Entity("jihadkhawaja.chat.shared.Models.Channel", b =>
                {
                    b.Property<Guid>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("uuid")
                        .HasColumnName("id");

                    b.Property<DateTimeOffset?>("DateCreated")
                        .IsRequired()
                        .HasColumnType("timestamp with time zone")
                        .HasColumnName("datecreated");

                    b.Property<DateTimeOffset?>("DateDeleted")
                        .HasColumnType("timestamp with time zone")
                        .HasColumnName("datedeleted");

                    b.Property<DateTimeOffset?>("DateUpdated")
                        .HasColumnType("timestamp with time zone")
                        .HasColumnName("dateupdated");

                    b.Property<bool>("IsEncrypted")
                        .HasColumnType("boolean")
                        .HasColumnName("isencrypted");

                    b.HasKey("Id");

                    b.ToTable("channels");
                });

            modelBuilder.Entity("jihadkhawaja.chat.shared.Models.ChannelUser", b =>
                {
                    b.Property<Guid>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("uuid")
                        .HasColumnName("id");

                    b.Property<Guid>("ChannelId")
                        .HasColumnType("uuid")
                        .HasColumnName("channelid");

                    b.Property<DateTimeOffset?>("DateCreated")
                        .IsRequired()
                        .HasColumnType("timestamp with time zone")
                        .HasColumnName("datecreated");

                    b.Property<DateTimeOffset?>("DateDeleted")
                        .HasColumnType("timestamp with time zone")
                        .HasColumnName("datedeleted");

                    b.Property<DateTimeOffset?>("DateUpdated")
                        .HasColumnType("timestamp with time zone")
                        .HasColumnName("dateupdated");

                    b.Property<bool>("IsAdmin")
                        .HasColumnType("boolean")
                        .HasColumnName("isadmin");

                    b.Property<bool>("IsEncrypted")
                        .HasColumnType("boolean")
                        .HasColumnName("isencrypted");

                    b.Property<Guid>("UserId")
                        .HasColumnType("uuid")
                        .HasColumnName("userid");

                    b.HasKey("Id");

                    b.ToTable("channelusers");
                });

            modelBuilder.Entity("jihadkhawaja.chat.shared.Models.Message", b =>
                {
                    b.Property<Guid>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("uuid")
                        .HasColumnName("id");

                    b.Property<Guid>("ChannelId")
                        .HasColumnType("uuid")
                        .HasColumnName("channelid");

                    b.Property<DateTimeOffset?>("DateCreated")
                        .IsRequired()
                        .HasColumnType("timestamp with time zone")
                        .HasColumnName("datecreated");

                    b.Property<DateTimeOffset?>("DateDeleted")
                        .HasColumnType("timestamp with time zone")
                        .HasColumnName("datedeleted");

                    b.Property<DateTimeOffset?>("DateSeen")
                        .HasColumnType("timestamp with time zone")
                        .HasColumnName("dateseen");

                    b.Property<DateTimeOffset?>("DateSent")
                        .HasColumnType("timestamp with time zone")
                        .HasColumnName("datesent");

                    b.Property<DateTimeOffset?>("DateUpdated")
                        .HasColumnType("timestamp with time zone")
                        .HasColumnName("dateupdated");

                    b.Property<bool>("IsEncrypted")
                        .HasColumnType("boolean")
                        .HasColumnName("isencrypted");

                    b.Property<Guid>("ReferenceId")
                        .HasColumnType("uuid")
                        .HasColumnName("referenceid");

                    b.Property<Guid>("SenderId")
                        .HasColumnType("uuid")
                        .HasColumnName("senderid");

                    b.HasKey("Id");

                    b.ToTable("messages");
                });

            modelBuilder.Entity("jihadkhawaja.chat.shared.Models.User", b =>
                {
                    b.Property<Guid>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("uuid")
                        .HasColumnName("id");

                    b.Property<string>("AvatarBase64")
                        .HasColumnType("text")
                        .HasColumnName("avatarbase64");

                    b.Property<string>("ConnectionId")
                        .HasColumnType("text")
                        .HasColumnName("connectionid");

                    b.Property<DateTimeOffset?>("DateCreated")
                        .IsRequired()
                        .HasColumnType("timestamp with time zone")
                        .HasColumnName("datecreated");

                    b.Property<DateTimeOffset?>("DateDeleted")
                        .HasColumnType("timestamp with time zone")
                        .HasColumnName("datedeleted");

                    b.Property<DateTimeOffset?>("DateUpdated")
                        .HasColumnType("timestamp with time zone")
                        .HasColumnName("dateupdated");

                    b.Property<bool>("InCall")
                        .HasColumnType("boolean")
                        .HasColumnName("incall");

                    b.Property<bool>("IsEncrypted")
                        .HasColumnType("boolean")
                        .HasColumnName("isencrypted");

                    b.Property<bool>("IsOnline")
                        .HasColumnType("boolean")
                        .HasColumnName("isonline");

                    b.Property<DateTimeOffset?>("LastLoginDate")
                        .HasColumnType("timestamp with time zone")
                        .HasColumnName("lastlogindate");

                    b.Property<string>("Password")
                        .IsRequired()
                        .HasColumnType("text")
                        .HasColumnName("password");

                    b.Property<string>("Role")
                        .IsRequired()
                        .HasColumnType("text")
                        .HasColumnName("role");

                    b.Property<string>("Username")
                        .IsRequired()
                        .HasColumnType("text")
                        .HasColumnName("username");

                    b.HasKey("Id");

                    b.HasIndex("Username")
                        .IsUnique();

                    b.ToTable("users");
                });

            modelBuilder.Entity("jihadkhawaja.chat.shared.Models.UserFriend", b =>
                {
                    b.Property<Guid>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("uuid")
                        .HasColumnName("id");

                    b.Property<DateTimeOffset?>("DateAcceptedOn")
                        .HasColumnType("timestamp with time zone")
                        .HasColumnName("dateacceptedon");

                    b.Property<DateTimeOffset?>("DateCreated")
                        .IsRequired()
                        .HasColumnType("timestamp with time zone")
                        .HasColumnName("datecreated");

                    b.Property<DateTimeOffset?>("DateDeleted")
                        .HasColumnType("timestamp with time zone")
                        .HasColumnName("datedeleted");

                    b.Property<DateTimeOffset?>("DateUpdated")
                        .HasColumnType("timestamp with time zone")
                        .HasColumnName("dateupdated");

                    b.Property<Guid>("FriendUserId")
                        .HasColumnType("uuid")
                        .HasColumnName("frienduserid");

                    b.Property<bool>("IsEncrypted")
                        .HasColumnType("boolean")
                        .HasColumnName("isencrypted");

                    b.Property<Guid>("UserId")
                        .HasColumnType("uuid")
                        .HasColumnName("userid");

                    b.HasKey("Id");

                    b.ToTable("usersfriends");
                });

            modelBuilder.Entity("jihadkhawaja.chat.shared.Models.UserPendingMessage", b =>
                {
                    b.Property<Guid>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("uuid")
                        .HasColumnName("id");

                    b.Property<string>("Content")
                        .HasColumnType("text")
                        .HasColumnName("content");

                    b.Property<DateTimeOffset?>("DateCreated")
                        .IsRequired()
                        .HasColumnType("timestamp with time zone")
                        .HasColumnName("datecreated");

                    b.Property<DateTimeOffset?>("DateDeleted")
                        .HasColumnType("timestamp with time zone")
                        .HasColumnName("datedeleted");

                    b.Property<DateTimeOffset?>("DateUpdated")
                        .HasColumnType("timestamp with time zone")
                        .HasColumnName("dateupdated");

                    b.Property<DateTimeOffset?>("DateUserReceivedOn")
                        .HasColumnType("timestamp with time zone")
                        .HasColumnName("dateuserreceivedon");

                    b.Property<bool>("IsEncrypted")
                        .HasColumnType("boolean")
                        .HasColumnName("isencrypted");

                    b.Property<Guid>("MessageId")
                        .HasColumnType("uuid")
                        .HasColumnName("messageid");

                    b.Property<Guid>("UserId")
                        .HasColumnType("uuid")
                        .HasColumnName("userid");

                    b.HasKey("Id");

                    b.ToTable("userspendingmessages");
                });
#pragma warning restore 612, 618
        }
    }
}
