using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FocusLens.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddQuestionEstimateAndChallengePostponement : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ChallengeDeferredUntilUtc",
                table: "StudySessionSelectedSections",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "EstimatedTimeMinutes",
                table: "StudySessionQuestions",
                type: "int",
                nullable: false,
                defaultValue: 1);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ChallengeDeferredUntilUtc",
                table: "StudySessionSelectedSections");

            migrationBuilder.DropColumn(
                name: "EstimatedTimeMinutes",
                table: "StudySessionQuestions");
        }
    }
}
