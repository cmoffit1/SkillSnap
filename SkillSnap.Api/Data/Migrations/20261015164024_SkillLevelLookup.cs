using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace SkillSnap.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class SkillLevelLookup : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SkillLevels",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SkillLevels", x => x.Id);
                });

            migrationBuilder.InsertData(
                table: "SkillLevels",
                columns: new[] { "Id", "Name", "SortOrder" },
                values: new object[,]
                {
                    { 1, "Beginner", 1 },
                    { 2, "Intermediate", 2 },
                    { 3, "Advanced", 3 },
                    { 4, "Expert", 4 }
                });

            migrationBuilder.AddColumn<int>(
                name: "SkillLevelId",
                table: "Skills",
                type: "int",
                nullable: false,
                defaultValue: 0);

            // Preserve existing data: add any non-standard level text as a new lookup row, then map skills to it.
            migrationBuilder.Sql(@"
                INSERT INTO SkillLevels (Name, SortOrder)
                SELECT LEFT(LTRIM(RTRIM(s.Level)), 50), 100 + ROW_NUMBER() OVER (ORDER BY LEFT(LTRIM(RTRIM(s.Level)), 50))
                FROM (SELECT DISTINCT LEFT(LTRIM(RTRIM(Level)), 50) AS Level FROM Skills
                      WHERE LTRIM(RTRIM(Level)) <> '') s
                WHERE NOT EXISTS (SELECT 1 FROM SkillLevels l WHERE l.Name = s.Level);

                UPDATE sk SET SkillLevelId = l.Id
                FROM Skills sk
                JOIN SkillLevels l ON l.Name = LEFT(LTRIM(RTRIM(sk.Level)), 50);

                UPDATE Skills SET SkillLevelId = 1 WHERE SkillLevelId = 0;");

            migrationBuilder.DropColumn(
                name: "Level",
                table: "Skills");

            migrationBuilder.CreateIndex(
                name: "IX_Skills_SkillLevelId",
                table: "Skills",
                column: "SkillLevelId");

            migrationBuilder.CreateIndex(
                name: "IX_SkillLevels_Name",
                table: "SkillLevels",
                column: "Name",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Skills_SkillLevels_SkillLevelId",
                table: "Skills",
                column: "SkillLevelId",
                principalTable: "SkillLevels",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Skills_SkillLevels_SkillLevelId",
                table: "Skills");

            migrationBuilder.DropTable(
                name: "SkillLevels");

            migrationBuilder.DropIndex(
                name: "IX_Skills_SkillLevelId",
                table: "Skills");

            migrationBuilder.DropColumn(
                name: "SkillLevelId",
                table: "Skills");

            migrationBuilder.AddColumn<string>(
                name: "Level",
                table: "Skills",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");
        }
    }
}
