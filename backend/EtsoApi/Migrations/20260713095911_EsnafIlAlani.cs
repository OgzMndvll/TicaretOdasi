using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EtsoApi.Migrations
{
    /// <inheritdoc />
    public partial class EsnafIlAlani : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Il",
                table: "Esnaflar",
                type: "longtext",
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            // Mevcut kayıtların tamamı Erzurum ilçelerine ait olduğu için ili Erzurum olarak doldurulur.
            migrationBuilder.Sql("UPDATE `Esnaflar` SET `Il` = 'Erzurum' WHERE `Il` IS NULL;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Il",
                table: "Esnaflar");
        }
    }
}
