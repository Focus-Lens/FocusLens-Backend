using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FocusLens.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddStudySessionSectionChallengeTiming : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ChallengeDueAtUtc",
                table: "StudySessionSelectedSections",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "CompletedAtUtc",
                table: "StudySessionSelectedSections",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Order",
                table: "StudySessionSelectedSections",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "StartedAtUtc",
                table: "StudySessionSelectedSections",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.Sql("""
                WITH RankedSections AS
                (
                    SELECT
                        Id,
                        ROW_NUMBER() OVER (
                            PARTITION BY StudySessionSelectionId
                            ORDER BY Id
                        ) AS NewOrder
                    FROM StudySessionSelectedSections
                )
                UPDATE sections
                SET [Order] = ranked.NewOrder
                FROM StudySessionSelectedSections sections
                INNER JOIN RankedSections ranked
                    ON sections.Id = ranked.Id;
            """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ChallengeDueAtUtc",
                table: "StudySessionSelectedSections");

            migrationBuilder.DropColumn(
                name: "CompletedAtUtc",
                table: "StudySessionSelectedSections");

            migrationBuilder.DropColumn(
                name: "Order",
                table: "StudySessionSelectedSections");

            migrationBuilder.DropColumn(
                name: "StartedAtUtc",
                table: "StudySessionSelectedSections");
        }
    }
}
