using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FocusLens.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class ReplaceSectionChallengeTimersWithPageTriggers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "ChallengeDueAtUtc",
                table: "StudySessionSelectedSections",
                newName: "ChallengeAvailableAtUtc");

            migrationBuilder.AddColumn<int>(
                name: "FromPage",
                table: "StudyMaterialSections",
                type: "int",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<int>(
                name: "ToPage",
                table: "StudyMaterialSections",
                type: "int",
                nullable: false,
                defaultValue: 1);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "FromPage",
                table: "StudyMaterialSections");

            migrationBuilder.DropColumn(
                name: "ToPage",
                table: "StudyMaterialSections");

            migrationBuilder.RenameColumn(
                name: "ChallengeAvailableAtUtc",
                table: "StudySessionSelectedSections",
                newName: "ChallengeDueAtUtc");
        }
    }
}
