using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SkillSnap.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class LinkAccountToPortfolio : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "PortfolioUserId",
                table: "AspNetUsers",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUsers_PortfolioUserId",
                table: "AspNetUsers",
                column: "PortfolioUserId",
                unique: true,
                filter: "[PortfolioUserId] IS NOT NULL");

            migrationBuilder.AddForeignKey(
                name: "FK_AspNetUsers_PortfolioUsers_PortfolioUserId",
                table: "AspNetUsers",
                column: "PortfolioUserId",
                principalTable: "PortfolioUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AspNetUsers_PortfolioUsers_PortfolioUserId",
                table: "AspNetUsers");

            migrationBuilder.DropIndex(
                name: "IX_AspNetUsers_PortfolioUserId",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "PortfolioUserId",
                table: "AspNetUsers");
        }
    }
}
