using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FocusLens.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddStudyTimeGoalDaysAndStartDate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "StudyTimeGoalDays",
                table: "Students",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "StudyTimeGoalStartDate",
                table: "Students",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "StudyTimeGoalDays",
                table: "ChildSetupDrafts",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "StudyTimeGoalStartDate",
                table: "ChildSetupDrafts",
                type: "date",
                nullable: true);
        migrationBuilder.Sql("""
            UPDATE Students
            SET StudyTimeGoalDays = '["Monday","Tuesday","Wednesday","Thursday","Friday"]'
            WHERE StudyTimeGoal_Period = 'Daily'
              AND StudyTimeGoalDays IS NULL;

            UPDATE ChildSetupDrafts
            SET StudyTimeGoalDays = '["Monday","Tuesday","Wednesday","Thursday","Friday"]'
            WHERE StudyTimeGoalPeriod = 'Daily'
              AND StudyTimeGoalDays IS NULL;
            """);

        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "StudyTimeGoalDays",
                table: "Students");

            migrationBuilder.DropColumn(
                name: "StudyTimeGoalStartDate",
                table: "Students");

            migrationBuilder.DropColumn(
                name: "StudyTimeGoalDays",
                table: "ChildSetupDrafts");

            migrationBuilder.DropColumn(
                name: "StudyTimeGoalStartDate",
                table: "ChildSetupDrafts");
        }
    }
}
