using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FocusLens.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddDigitalSessionInterventions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ExtendedDurationMinutes",
                table: "StudySessions",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<bool>(
                name: "IsSlowPaceEnabled",
                table: "StudySessions",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "BreakEndsAtUtc",
                table: "StudySessions",
                type: "datetimeoffset",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ExtendedDurationMinutes",
                table: "StudySessions");

            migrationBuilder.DropColumn(
                name: "IsSlowPaceEnabled",
                table: "StudySessions");

            migrationBuilder.DropColumn(
                name: "BreakEndsAtUtc",
                table: "StudySessions");
        }
    }
}
