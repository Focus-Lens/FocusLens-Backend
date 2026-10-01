using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FocusLens.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddStudentWeekStartsOn : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "WeekStartsOn",
                table: "Students",
                type: "varchar(20)",
                maxLength: 20,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "WeekStartsOn",
                table: "Students");
        }
    }
}
