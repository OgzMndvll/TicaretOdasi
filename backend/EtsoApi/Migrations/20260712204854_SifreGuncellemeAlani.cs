using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EtsoApi.Migrations
{
    /// <inheritdoc />
    public partial class SifreGuncellemeAlani : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Mevcut kullanıcılar uzak geçmişte bir tarih alır; böylece migration eldeki oturumları düşürmez.
            // (0001-01-01 kullanılamaz: MySQL DATETIME aralığının dışında ve tarih aritmetiğinde taşmaya yol açar.)
            migrationBuilder.AddColumn<DateTime>(
                name: "SifreGuncelleme",
                table: "Kullanicilar",
                type: "datetime(6)",
                nullable: false,
                defaultValue: new DateTime(2000, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SifreGuncelleme",
                table: "Kullanicilar");
        }
    }
}
