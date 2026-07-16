using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EtsoApi.Migrations
{
    /// <inheritdoc />
    public partial class KullaniciSifreAlanlari : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "BasarisizGiris",
                table: "Kullanicilar",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "KilitBitis",
                table: "Kullanicilar",
                type: "datetime(6)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SifreHash",
                table: "Kullanicilar",
                type: "longtext",
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "BasarisizGiris",
                table: "Kullanicilar");

            migrationBuilder.DropColumn(
                name: "KilitBitis",
                table: "Kullanicilar");

            migrationBuilder.DropColumn(
                name: "SifreHash",
                table: "Kullanicilar");
        }
    }
}
