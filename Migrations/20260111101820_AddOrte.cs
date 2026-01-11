using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MaestroNotes.Migrations
{
    /// <inheritdoc />
    public partial class AddOrte : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
/*            migrationBuilder.DropColumn(
                name: "Bewertung1",
                table: "MusicRecords");

            migrationBuilder.DropColumn(
                name: "Bewertung2",
                table: "MusicRecords");

            migrationBuilder.DropColumn(
                name: "Dirigent",
                table: "MusicRecords");

            migrationBuilder.DropColumn(
                name: "Komponist",
                table: "MusicRecords");

            migrationBuilder.DropColumn(
                name: "Orchester",
                table: "MusicRecords");

            migrationBuilder.DropColumn(
                name: "Solist",
                table: "MusicRecords");

            migrationBuilder.DropColumn(
                name: "Werk",
                table: "MusicRecords");
*/
            migrationBuilder.AddColumn<int>(
                name: "OrtId",
                table: "MusicRecords",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Orte",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    Name = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Note = table.Column<string>(type: "varchar(2000)", maxLength: 2000, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Orte", x => x.Id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_MusicRecords_OrtId",
                table: "MusicRecords",
                column: "OrtId");

            migrationBuilder.AddForeignKey(
                name: "FK_MusicRecords_Orte_OrtId",
                table: "MusicRecords",
                column: "OrtId",
                principalTable: "Orte",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_MusicRecords_Orte_OrtId",
                table: "MusicRecords");

            migrationBuilder.DropTable(
                name: "Orte");

            migrationBuilder.DropIndex(
                name: "IX_MusicRecords_OrtId",
                table: "MusicRecords");

            migrationBuilder.DropColumn(
                name: "OrtId",
                table: "MusicRecords");
        }
    }
}
