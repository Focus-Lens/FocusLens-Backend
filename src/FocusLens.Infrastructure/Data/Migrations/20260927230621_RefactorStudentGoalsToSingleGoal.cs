using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FocusLens.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class RefactorStudentGoalsToSingleGoal : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Goals",
                table: "Students");

            migrationBuilder.DropColumn(
                name: "StudyPriorities",
                table: "Students");

            migrationBuilder.DropColumn(
                name: "StudyPriorities",
                table: "ChildSetupDrafts");

            migrationBuilder.AddColumn<string>(
                name: "Goal",
                table: "Students",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Goal",
                table: "ChildSetupDrafts",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Goal",
                table: "Students");

            migrationBuilder.DropColumn(
                name: "Goal",
                table: "ChildSetupDrafts");

            migrationBuilder.AddColumn<string>(
                name: "Goals",
                table: "Students",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "StudyPriorities",
                table: "Students",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "StudyPriorities",
                table: "ChildSetupDrafts",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");
        }
    }
}
