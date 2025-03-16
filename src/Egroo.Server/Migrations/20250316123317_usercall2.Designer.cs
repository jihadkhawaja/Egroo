﻿// <auto-generated />
using System;
using System.Collections.Generic;
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
    [Migration("20250316123317_usercall2")]
    partial class usercall2
    {
        /// <inheritdoc />
        protected override void BuildTargetModel(ModelBuilder modelBuilder)
        {
#pragma warning disable 612, 618
            modelBuilder
                .HasAnnotation("ProductVersion", "8.0.14")
                .HasAnnotation("Relational:MaxIdentifierLength", 63);

            NpgsqlModelBuilderExtensions.UseIdentityByDefaultColumns(modelBuilder);

            modelBuilder.Entity("jihadkhawaja.chat.server.Models.User", b =>
                {
                    b.Property<Guid>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("uuid")
                        .HasColumnName("id");

                    b.Property<Guid?>("CreatedBy")
                        .HasColumnType("uuid")
                        .HasColumnName("createdby");

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

                    b.Property<Guid?>("DeletedBy")
                        .HasColumnType("uuid")
                        .HasColumnName("deletedby");

                    b.Property<bool>("IsEncrypted")
                        .HasColumnType("boolean")
                        .HasColumnName("isencrypted");

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

                    b.Property<Guid?>("UpdatedBy")
                        .HasColumnType("uuid")
                        .HasColumnName("updatedby");

                    b.Property<string>("Username")
                        .IsRequired()
                        .HasColumnType("text")
                        .HasColumnName("username");

                    b.HasKey("Id");

                    b.HasIndex("Username")
                        .IsUnique();

                    b.ToTable("users");
                });

            modelBuilder.Entity("jihadkhawaja.chat.server.Models.UserSecurity", b =>
                {
                    b.Property<Guid>("UserId")
                        .HasColumnType("uuid")
                        .HasColumnName("userid");

                    b.Property<bool>("IsTwoFactorEnabled")
                        .HasColumnType("boolean")
                        .HasColumnName("istwofactorenabled");

                    b.HasKey("UserId");

                    b.ToTable("usersecurity");
                });

            modelBuilder.Entity("jihadkhawaja.chat.shared.Models.Channel", b =>
                {
                    b.Property<Guid>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("uuid")
                        .HasColumnName("id");

                    b.Property<Guid?>("CreatedBy")
                        .HasColumnType("uuid")
                        .HasColumnName("createdby");

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

                    b.Property<Guid?>("DeletedBy")
                        .HasColumnType("uuid")
                        .HasColumnName("deletedby");

                    b.Property<bool>("IsEncrypted")
                        .HasColumnType("boolean")
                        .HasColumnName("isencrypted");

                    b.Property<bool>("IsPublic")
                        .HasColumnType("boolean")
                        .HasColumnName("ispublic");

                    b.Property<string>("Title")
                        .HasColumnType("text")
                        .HasColumnName("title");

                    b.Property<Guid?>("UpdatedBy")
                        .HasColumnType("uuid")
                        .HasColumnName("updatedby");

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

                    b.Property<Guid?>("CreatedBy")
                        .HasColumnType("uuid")
                        .HasColumnName("createdby");

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

                    b.Property<Guid?>("DeletedBy")
                        .HasColumnType("uuid")
                        .HasColumnName("deletedby");

                    b.Property<bool>("IsAdmin")
                        .HasColumnType("boolean")
                        .HasColumnName("isadmin");

                    b.Property<bool>("IsEncrypted")
                        .HasColumnType("boolean")
                        .HasColumnName("isencrypted");

                    b.Property<Guid?>("UpdatedBy")
                        .HasColumnType("uuid")
                        .HasColumnName("updatedby");

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

                    b.Property<Guid?>("CreatedBy")
                        .HasColumnType("uuid")
                        .HasColumnName("createdby");

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

                    b.Property<Guid?>("DeletedBy")
                        .HasColumnType("uuid")
                        .HasColumnName("deletedby");

                    b.Property<bool>("IsEncrypted")
                        .HasColumnType("boolean")
                        .HasColumnName("isencrypted");

                    b.Property<Guid>("ReferenceId")
                        .HasColumnType("uuid")
                        .HasColumnName("referenceid");

                    b.Property<Guid>("SenderId")
                        .HasColumnType("uuid")
                        .HasColumnName("senderid");

                    b.Property<Guid?>("UpdatedBy")
                        .HasColumnType("uuid")
                        .HasColumnName("updatedby");

                    b.HasKey("Id");

                    b.ToTable("messages");
                });

            modelBuilder.Entity("jihadkhawaja.chat.shared.Models.UserDetail", b =>
                {
                    b.Property<Guid>("UserId")
                        .HasColumnType("uuid")
                        .HasColumnName("userid");

                    b.Property<string>("Country")
                        .HasColumnType("text")
                        .HasColumnName("country");

                    b.Property<string>("DisplayName")
                        .HasColumnType("text")
                        .HasColumnName("displayname");

                    b.Property<string>("Email")
                        .HasColumnType("text")
                        .HasColumnName("email");

                    b.Property<string>("FirstName")
                        .HasColumnType("text")
                        .HasColumnName("firstname");

                    b.Property<string>("FullDescription")
                        .HasColumnType("text")
                        .HasColumnName("fulldescription");

                    b.Property<string>("Interests")
                        .HasColumnType("text")
                        .HasColumnName("interests");

                    b.Property<string>("LastName")
                        .HasColumnType("text")
                        .HasColumnName("lastname");

                    b.Property<string>("PhoneCountryCode")
                        .HasColumnType("text")
                        .HasColumnName("phonecountrycode");

                    b.Property<string>("PhoneNumber")
                        .HasColumnType("text")
                        .HasColumnName("phonenumber");

                    b.Property<string>("Pronounce")
                        .HasColumnType("text")
                        .HasColumnName("pronounce");

                    b.Property<string>("Region")
                        .HasColumnType("text")
                        .HasColumnName("region");

                    b.Property<int>("Sex")
                        .HasColumnType("integer")
                        .HasColumnName("sex");

                    b.Property<string>("ShortDescription")
                        .HasColumnType("text")
                        .HasColumnName("shortdescription");

                    b.Property<List<string>>("SocialLinks")
                        .HasColumnType("text[]")
                        .HasColumnName("sociallinks");

                    b.HasKey("UserId");

                    b.ToTable("userdetail");
                });

            modelBuilder.Entity("jihadkhawaja.chat.shared.Models.UserFeedback", b =>
                {
                    b.Property<Guid>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("uuid")
                        .HasColumnName("id");

                    b.Property<Guid?>("CreatedBy")
                        .HasColumnType("uuid")
                        .HasColumnName("createdby");

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

                    b.Property<Guid?>("DeletedBy")
                        .HasColumnType("uuid")
                        .HasColumnName("deletedby");

                    b.Property<bool>("IsEncrypted")
                        .HasColumnType("boolean")
                        .HasColumnName("isencrypted");

                    b.Property<string>("Text")
                        .IsRequired()
                        .HasColumnType("text")
                        .HasColumnName("text");

                    b.Property<Guid?>("UpdatedBy")
                        .HasColumnType("uuid")
                        .HasColumnName("updatedby");

                    b.Property<Guid>("UserId")
                        .HasColumnType("uuid")
                        .HasColumnName("userid");

                    b.HasKey("Id");

                    b.HasIndex("UserId");

                    b.ToTable("userfeedback");
                });

            modelBuilder.Entity("jihadkhawaja.chat.shared.Models.UserFriend", b =>
                {
                    b.Property<Guid>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("uuid")
                        .HasColumnName("id");

                    b.Property<Guid?>("CreatedBy")
                        .HasColumnType("uuid")
                        .HasColumnName("createdby");

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

                    b.Property<Guid?>("DeletedBy")
                        .HasColumnType("uuid")
                        .HasColumnName("deletedby");

                    b.Property<Guid>("FriendUserId")
                        .HasColumnType("uuid")
                        .HasColumnName("frienduserid");

                    b.Property<bool>("IsEncrypted")
                        .HasColumnType("boolean")
                        .HasColumnName("isencrypted");

                    b.Property<Guid?>("UpdatedBy")
                        .HasColumnType("uuid")
                        .HasColumnName("updatedby");

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

                    b.Property<Guid?>("CreatedBy")
                        .HasColumnType("uuid")
                        .HasColumnName("createdby");

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

                    b.Property<Guid?>("DeletedBy")
                        .HasColumnType("uuid")
                        .HasColumnName("deletedby");

                    b.Property<bool>("IsEncrypted")
                        .HasColumnType("boolean")
                        .HasColumnName("isencrypted");

                    b.Property<Guid>("MessageId")
                        .HasColumnType("uuid")
                        .HasColumnName("messageid");

                    b.Property<Guid?>("UpdatedBy")
                        .HasColumnType("uuid")
                        .HasColumnName("updatedby");

                    b.Property<Guid>("UserId")
                        .HasColumnType("uuid")
                        .HasColumnName("userid");

                    b.HasKey("Id");

                    b.ToTable("userspendingmessages");
                });

            modelBuilder.Entity("jihadkhawaja.chat.shared.Models.UserStorage", b =>
                {
                    b.Property<Guid>("UserId")
                        .HasColumnType("uuid")
                        .HasColumnName("userid");

                    b.Property<string>("AvatarContentType")
                        .HasColumnType("text")
                        .HasColumnName("avatarcontenttype");

                    b.Property<string>("AvatarImageBase64")
                        .HasColumnType("text")
                        .HasColumnName("avatarimagebase64");

                    b.Property<string>("CoverContentType")
                        .HasColumnType("text")
                        .HasColumnName("covercontenttype");

                    b.Property<string>("CoverImageBase64")
                        .HasColumnType("text")
                        .HasColumnName("coverimagebase64");

                    b.HasKey("UserId");

                    b.ToTable("userstorage");
                });

            modelBuilder.Entity("jihadkhawaja.chat.server.Models.UserSecurity", b =>
                {
                    b.HasOne("jihadkhawaja.chat.server.Models.User", null)
                        .WithOne("UserSecuriy")
                        .HasForeignKey("jihadkhawaja.chat.server.Models.UserSecurity", "UserId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();
                });

            modelBuilder.Entity("jihadkhawaja.chat.shared.Models.UserDetail", b =>
                {
                    b.HasOne("jihadkhawaja.chat.server.Models.User", null)
                        .WithOne("UserDetail")
                        .HasForeignKey("jihadkhawaja.chat.shared.Models.UserDetail", "UserId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();
                });

            modelBuilder.Entity("jihadkhawaja.chat.shared.Models.UserFeedback", b =>
                {
                    b.HasOne("jihadkhawaja.chat.server.Models.User", null)
                        .WithMany("UserFeedbacks")
                        .HasForeignKey("UserId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();
                });

            modelBuilder.Entity("jihadkhawaja.chat.shared.Models.UserStorage", b =>
                {
                    b.HasOne("jihadkhawaja.chat.server.Models.User", null)
                        .WithOne("UserStorage")
                        .HasForeignKey("jihadkhawaja.chat.shared.Models.UserStorage", "UserId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();
                });

            modelBuilder.Entity("jihadkhawaja.chat.server.Models.User", b =>
                {
                    b.Navigation("UserDetail");

                    b.Navigation("UserFeedbacks");

                    b.Navigation("UserSecuriy");

                    b.Navigation("UserStorage");
                });
#pragma warning restore 612, 618
        }
    }
}
