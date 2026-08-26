using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EtsoApi.Migrations
{
    /// <inheritdoc />
    public partial class GorusmeIkinciGorevli : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "IkinciGorevliId",
                table: "Gorusmeler",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Gorusmeler_IkinciGorevliId",
                table: "Gorusmeler",
                column: "IkinciGorevliId");

            migrationBuilder.AddForeignKey(
                name: "FK_Gorusmeler_Kullanicilar_IkinciGorevliId",
                table: "Gorusmeler",
                column: "IkinciGorevliId",
                principalTable: "Kullanicilar",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Gorusmeler_Kullanicilar_IkinciGorevliId",
                table: "Gorusmeler");

            migrationBuilder.DropIndex(
                name: "IX_Gorusmeler_IkinciGorevliId",
                table: "Gorusmeler");

            migrationBuilder.DropColumn(
                name: "IkinciGorevliId",
                table: "Gorusmeler");
        }
    }
}
