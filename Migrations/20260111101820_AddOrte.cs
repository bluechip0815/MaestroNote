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

            // Data Migration: Populate Orte from distinct strings
            migrationBuilder.Sql("INSERT INTO Orte (Name) SELECT DISTINCT Ort FROM MusicRecords WHERE Ort IS NOT NULL AND Ort <> '';");

            // Data Migration: Link MusicRecords to Orte
            migrationBuilder.Sql("UPDATE MusicRecords m JOIN Orte o ON m.Ort = o.Name SET m.OrtId = o.Id;");

            // Drop legacy column
            migrationBuilder.DropColumn(
                name: "Ort",
                table: "MusicRecords");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Ort",
                table: "MusicRecords",
                type: "varchar(64)",
                maxLength: 64,
                nullable: false,
                defaultValue: "")
                .Annotation("MySql:CharSet", "utf8mb4");

            // Restore data
            migrationBuilder.Sql("UPDATE MusicRecords m JOIN Orte o ON m.OrtId = o.Id SET m.Ort = o.Name;");

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
