using EtsoApi.Data;
using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EtsoApi.Migrations
{
    [DbContext(typeof(EtsoDbContext))]
    [Migration("20260825120000_GorevlendirmeKararAkisi")]
    public partial class GorevlendirmeKararAkisi : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "KararTarihi",
                table: "Gorevlendirmeler",
                type: "datetime(6)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RedMazereti",
                table: "Gorevlendirmeler",
                type: "varchar(1000)",
                maxLength: 1000,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "KararTarihi",
                table: "Gorevlendirmeler");

            migrationBuilder.DropColumn(
                name: "RedMazereti",
                table: "Gorevlendirmeler");
        }
    }
}
