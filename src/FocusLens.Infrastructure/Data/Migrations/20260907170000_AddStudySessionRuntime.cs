using FocusLens.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FocusLens.Infrastructure.Data.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20260907170000_AddStudySessionRuntime")]
public partial class AddStudySessionRuntime : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<int>(
            name: "AccumulatedPausedSeconds",
            table: "StudySessions",
            type: "int",
            nullable: false,
            defaultValue: 0);

        migrationBuilder.AddColumn<DateTimeOffset>(
            name: "CancelledAtUtc",
            table: "StudySessions",
            type: "datetimeoffset",
            nullable: true);

        migrationBuilder.AddColumn<DateTimeOffset>(
            name: "CompletedAtUtc",
            table: "StudySessions",
            type: "datetimeoffset",
            nullable: true);

        migrationBuilder.AddColumn<DateTimeOffset>(
            name: "PausedAtUtc",
            table: "StudySessions",
            type: "datetimeoffset",
            nullable: true);

    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(name: "AccumulatedPausedSeconds", table: "StudySessions");
        migrationBuilder.DropColumn(name: "CancelledAtUtc", table: "StudySessions");
        migrationBuilder.DropColumn(name: "CompletedAtUtc", table: "StudySessions");
        migrationBuilder.DropColumn(name: "PausedAtUtc", table: "StudySessions");
    }
}
