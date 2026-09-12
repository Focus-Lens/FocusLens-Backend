using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FocusLens.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class StudentProfileExpansion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateOnly>(
                name: "DateOfBirth",
                table: "Students",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Goals",
                table: "Students",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "[]");

            migrationBuilder.AddColumn<string>(
                name: "StudyPriorities",
                table: "Students",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "[]");

            migrationBuilder.AddColumn<string>(
                name: "StudyTimeGoal_Period",
                table: "Students",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "StudyTimeGoal_TargetMinutes",
                table: "Students",
                type: "int",
                nullable: true);

            migrationBuilder.Sql(
                "UPDATE Students " +
                "SET Goals = JSON_MODIFY(NCHAR(91) + NCHAR(93), " +
                "NCHAR(36) + NCHAR(91) + NCHAR(48) + NCHAR(93), Goal) " +
                "WHERE Goal IS NOT NULL;");

            migrationBuilder.DropColumn(
                name: "Goal",
                table: "Students");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Goal",
                table: "Students",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.Sql(
                "UPDATE Students " +
                "SET Goal = JSON_VALUE(Goals, " +
                "NCHAR(36) + NCHAR(91) + NCHAR(48) + NCHAR(93)) " +
                "WHERE ISJSON(Goals) = 1;");

            migrationBuilder.DropColumn(
                name: "DateOfBirth",
                table: "Students");

            migrationBuilder.DropColumn(
                name: "Goals",
                table: "Students");

            migrationBuilder.DropColumn(
                name: "StudyPriorities",
                table: "Students");

            migrationBuilder.DropColumn(
                name: "StudyTimeGoal_Period",
                table: "Students");

            migrationBuilder.DropColumn(
                name: "StudyTimeGoal_TargetMinutes",
                table: "Students");
        }
    }
}
