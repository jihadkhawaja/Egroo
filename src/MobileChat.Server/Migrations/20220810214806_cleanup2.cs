﻿using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MobileChat.Server.Migrations
{
    public partial class cleanup2 : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Title",
                table: "Channels");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Title",
                table: "Channels",
                type: "text",
                nullable: false,
                defaultValue: "");
        }
    }
}