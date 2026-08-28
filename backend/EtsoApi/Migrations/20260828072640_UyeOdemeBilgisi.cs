using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EtsoApi.Migrations
{
    /// <inheritdoc />
    public partial class UyeOdemeBilgisi : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "OdemeTarihi",
                table: "Esnaflar",
                type: "datetime(6)",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "Odendi",
                table: "Esnaflar",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "OdemeTarihi",
                table: "Esnaflar");

            migrationBuilder.DropColumn(
                name: "Odendi",
                table: "Esnaflar");
        }
    }
}
