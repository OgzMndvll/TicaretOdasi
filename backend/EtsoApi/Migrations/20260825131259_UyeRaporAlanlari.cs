using System;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EtsoApi.Migrations
{
    /// <inheritdoc />
    public partial class UyeRaporAlanlari : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "Ad",
                table: "Gruplar",
                type: "varchar(180)",
                maxLength: 180,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "varchar(120)",
                oldMaxLength: 120)
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<int>(
                name: "No",
                table: "Gruplar",
                type: "int",
                nullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Isletme",
                table: "Esnaflar",
                type: "varchar(250)",
                maxLength: 250,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "varchar(160)",
                oldMaxLength: 160)
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "Derece",
                table: "Esnaflar",
                type: "varchar(20)",
                maxLength: 20,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "DurumDegisimNedeni",
                table: "Esnaflar",
                type: "varchar(200)",
                maxLength: 200,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<DateTime>(
                name: "DurumDegisimTarihi",
                table: "Esnaflar",
                type: "datetime(6)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FaaliyetDetayi",
                table: "Esnaflar",
                type: "varchar(2000)",
                maxLength: 2000,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "Gorevi",
                table: "Esnaflar",
                type: "varchar(80)",
                maxLength: 80,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "IsTelefonu",
                table: "Esnaflar",
                type: "varchar(30)",
                maxLength: 30,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<DateTime>(
                name: "KurulusTarihi",
                table: "Esnaflar",
                type: "datetime(6)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "NaceAdi",
                table: "Esnaflar",
                type: "varchar(500)",
                maxLength: 500,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "NaceKodu",
                table: "Esnaflar",
                type: "varchar(20)",
                maxLength: 20,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<DateTime>(
                name: "OdaKararTarihi",
                table: "Esnaflar",
                type: "datetime(6)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Sermaye",
                table: "Esnaflar",
                type: "varchar(30)",
                maxLength: 30,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "SirketTipi",
                table: "Esnaflar",
                type: "varchar(60)",
                maxLength: 60,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "TabelaUnvani",
                table: "Esnaflar",
                type: "varchar(160)",
                maxLength: 160,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "TicaretSicilNo",
                table: "Esnaflar",
                type: "varchar(30)",
                maxLength: 30,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "UyeSicilNo",
                table: "Esnaflar",
                type: "varchar(20)",
                maxLength: 20,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "UyelikDurumu",
                table: "Esnaflar",
                type: "varchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "")
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "Uyruk",
                table: "Esnaflar",
                type: "varchar(40)",
                maxLength: 40,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "VergiDairesi",
                table: "Esnaflar",
                type: "varchar(80)",
                maxLength: 80,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<DateTime>(
                name: "VergiTerkTarihi",
                table: "Esnaflar",
                type: "datetime(6)",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "EsnafYetkilileri",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    EsnafId = table.Column<int>(type: "int", nullable: false),
                    AdSoyad = table.Column<string>(type: "varchar(120)", maxLength: 120, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Gorevi = table.Column<string>(type: "varchar(80)", maxLength: 80, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    YetkiBaslangic = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    YetkiBitis = table.Column<DateTime>(type: "datetime(6)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EsnafYetkilileri", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EsnafYetkilileri_Esnaflar_EsnafId",
                        column: x => x.EsnafId,
                        principalTable: "Esnaflar",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_Gruplar_No",
                table: "Gruplar",
                column: "No",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Esnaflar_UyelikDurumu",
                table: "Esnaflar",
                column: "UyelikDurumu");

            migrationBuilder.CreateIndex(
                name: "IX_Esnaflar_UyeSicilNo",
                table: "Esnaflar",
                column: "UyeSicilNo",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_EsnafYetkilileri_EsnafId",
                table: "EsnafYetkilileri",
                column: "EsnafId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "EsnafYetkilileri");

            migrationBuilder.DropIndex(
                name: "IX_Gruplar_No",
                table: "Gruplar");

            migrationBuilder.DropIndex(
                name: "IX_Esnaflar_UyelikDurumu",
                table: "Esnaflar");

            migrationBuilder.DropIndex(
                name: "IX_Esnaflar_UyeSicilNo",
                table: "Esnaflar");

            migrationBuilder.DropColumn(
                name: "No",
                table: "Gruplar");

            migrationBuilder.DropColumn(
                name: "Derece",
                table: "Esnaflar");

            migrationBuilder.DropColumn(
                name: "DurumDegisimNedeni",
                table: "Esnaflar");

            migrationBuilder.DropColumn(
                name: "DurumDegisimTarihi",
                table: "Esnaflar");

            migrationBuilder.DropColumn(
                name: "FaaliyetDetayi",
                table: "Esnaflar");

            migrationBuilder.DropColumn(
                name: "Gorevi",
                table: "Esnaflar");

            migrationBuilder.DropColumn(
                name: "IsTelefonu",
                table: "Esnaflar");

            migrationBuilder.DropColumn(
                name: "KurulusTarihi",
                table: "Esnaflar");

            migrationBuilder.DropColumn(
                name: "NaceAdi",
                table: "Esnaflar");

            migrationBuilder.DropColumn(
                name: "NaceKodu",
                table: "Esnaflar");

            migrationBuilder.DropColumn(
                name: "OdaKararTarihi",
                table: "Esnaflar");

            migrationBuilder.DropColumn(
                name: "Sermaye",
                table: "Esnaflar");

            migrationBuilder.DropColumn(
                name: "SirketTipi",
                table: "Esnaflar");

            migrationBuilder.DropColumn(
                name: "TabelaUnvani",
                table: "Esnaflar");

            migrationBuilder.DropColumn(
                name: "TicaretSicilNo",
                table: "Esnaflar");

            migrationBuilder.DropColumn(
                name: "UyeSicilNo",
                table: "Esnaflar");

            migrationBuilder.DropColumn(
                name: "UyelikDurumu",
                table: "Esnaflar");

            migrationBuilder.DropColumn(
                name: "Uyruk",
                table: "Esnaflar");

            migrationBuilder.DropColumn(
                name: "VergiDairesi",
                table: "Esnaflar");

            migrationBuilder.DropColumn(
                name: "VergiTerkTarihi",
                table: "Esnaflar");

            migrationBuilder.AlterColumn<string>(
                name: "Ad",
                table: "Gruplar",
                type: "varchar(120)",
                maxLength: 120,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "varchar(180)",
                oldMaxLength: 180)
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AlterColumn<string>(
                name: "Isletme",
                table: "Esnaflar",
                type: "varchar(160)",
                maxLength: 160,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "varchar(250)",
                oldMaxLength: 250)
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");
        }
    }
}
