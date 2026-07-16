using System;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EtsoApi.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterDatabase()
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "Gruplar",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    Ad = table.Column<string>(type: "varchar(120)", maxLength: 120, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Aciklama = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Tur = table.Column<string>(type: "varchar(40)", maxLength: 40, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    UstGrupId = table.Column<int>(type: "int", nullable: true),
                    Durum = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    GuncellemeTarihi = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Gruplar", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Gruplar_Gruplar_UstGrupId",
                        column: x => x.UstGrupId,
                        principalTable: "Gruplar",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "Kullanicilar",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    AdSoyad = table.Column<string>(type: "varchar(120)", maxLength: 120, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    KullaniciAdi = table.Column<string>(type: "varchar(60)", maxLength: 60, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Rol = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Gorev = table.Column<string>(type: "varchar(120)", maxLength: 120, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Birim = table.Column<string>(type: "varchar(120)", maxLength: 120, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Eposta = table.Column<string>(type: "varchar(160)", maxLength: 160, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Telefon = table.Column<string>(type: "varchar(30)", maxLength: 30, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Durum = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    OlusturmaTarihi = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Kullanicilar", x => x.Id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "Esnaflar",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    AdSoyad = table.Column<string>(type: "varchar(120)", maxLength: 120, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Isletme = table.Column<string>(type: "varchar(160)", maxLength: 160, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    VergiNo = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    GrupId = table.Column<int>(type: "int", nullable: true),
                    Ilce = table.Column<string>(type: "varchar(60)", maxLength: 60, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Mahalle = table.Column<string>(type: "varchar(80)", maxLength: 80, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Telefon = table.Column<string>(type: "varchar(30)", maxLength: 30, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    GorevliId = table.Column<int>(type: "int", nullable: true),
                    Durum = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    SonGorusmeTarihi = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    KayitTarihi = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Esnaflar", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Esnaflar_Gruplar_GrupId",
                        column: x => x.GrupId,
                        principalTable: "Gruplar",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_Esnaflar_Kullanicilar_GorevliId",
                        column: x => x.GorevliId,
                        principalTable: "Kullanicilar",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "Gorevlendirmeler",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    GorevliId = table.Column<int>(type: "int", nullable: false),
                    EsnafId = table.Column<int>(type: "int", nullable: true),
                    GrupId = table.Column<int>(type: "int", nullable: true),
                    Tarih = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    Not = table.Column<string>(type: "varchar(1000)", maxLength: 1000, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Durum = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Gorevlendirmeler", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Gorevlendirmeler_Esnaflar_EsnafId",
                        column: x => x.EsnafId,
                        principalTable: "Esnaflar",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Gorevlendirmeler_Gruplar_GrupId",
                        column: x => x.GrupId,
                        principalTable: "Gruplar",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_Gorevlendirmeler_Kullanicilar_GorevliId",
                        column: x => x.GorevliId,
                        principalTable: "Kullanicilar",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "Gorusmeler",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    EsnafId = table.Column<int>(type: "int", nullable: false),
                    GorevliId = table.Column<int>(type: "int", nullable: false),
                    Tarih = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    Sonuc = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Not = table.Column<string>(type: "varchar(1000)", maxLength: 1000, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    TakipGerekli = table.Column<bool>(type: "tinyint(1)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Gorusmeler", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Gorusmeler_Esnaflar_EsnafId",
                        column: x => x.EsnafId,
                        principalTable: "Esnaflar",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Gorusmeler_Kullanicilar_GorevliId",
                        column: x => x.GorevliId,
                        principalTable: "Kullanicilar",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "Onaylar",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    EsnafId = table.Column<int>(type: "int", nullable: false),
                    GorevliId = table.Column<int>(type: "int", nullable: true),
                    IslemTuru = table.Column<string>(type: "varchar(80)", maxLength: 80, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Tarih = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    Durum = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Onaylar", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Onaylar_Esnaflar_EsnafId",
                        column: x => x.EsnafId,
                        principalTable: "Esnaflar",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Onaylar_Kullanicilar_GorevliId",
                        column: x => x.GorevliId,
                        principalTable: "Kullanicilar",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_Esnaflar_Durum",
                table: "Esnaflar",
                column: "Durum");

            migrationBuilder.CreateIndex(
                name: "IX_Esnaflar_GorevliId",
                table: "Esnaflar",
                column: "GorevliId");

            migrationBuilder.CreateIndex(
                name: "IX_Esnaflar_GrupId",
                table: "Esnaflar",
                column: "GrupId");

            migrationBuilder.CreateIndex(
                name: "IX_Gorevlendirmeler_EsnafId",
                table: "Gorevlendirmeler",
                column: "EsnafId");

            migrationBuilder.CreateIndex(
                name: "IX_Gorevlendirmeler_GorevliId",
                table: "Gorevlendirmeler",
                column: "GorevliId");

            migrationBuilder.CreateIndex(
                name: "IX_Gorevlendirmeler_GrupId",
                table: "Gorevlendirmeler",
                column: "GrupId");

            migrationBuilder.CreateIndex(
                name: "IX_Gorusmeler_EsnafId",
                table: "Gorusmeler",
                column: "EsnafId");

            migrationBuilder.CreateIndex(
                name: "IX_Gorusmeler_GorevliId",
                table: "Gorusmeler",
                column: "GorevliId");

            migrationBuilder.CreateIndex(
                name: "IX_Gorusmeler_Tarih",
                table: "Gorusmeler",
                column: "Tarih");

            migrationBuilder.CreateIndex(
                name: "IX_Gruplar_Ad",
                table: "Gruplar",
                column: "Ad",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Gruplar_UstGrupId",
                table: "Gruplar",
                column: "UstGrupId");

            migrationBuilder.CreateIndex(
                name: "IX_Kullanicilar_KullaniciAdi",
                table: "Kullanicilar",
                column: "KullaniciAdi",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Onaylar_Durum",
                table: "Onaylar",
                column: "Durum");

            migrationBuilder.CreateIndex(
                name: "IX_Onaylar_EsnafId",
                table: "Onaylar",
                column: "EsnafId");

            migrationBuilder.CreateIndex(
                name: "IX_Onaylar_GorevliId",
                table: "Onaylar",
                column: "GorevliId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Gorevlendirmeler");

            migrationBuilder.DropTable(
                name: "Gorusmeler");

            migrationBuilder.DropTable(
                name: "Onaylar");

            migrationBuilder.DropTable(
                name: "Esnaflar");

            migrationBuilder.DropTable(
                name: "Gruplar");

            migrationBuilder.DropTable(
                name: "Kullanicilar");
        }
    }
}
