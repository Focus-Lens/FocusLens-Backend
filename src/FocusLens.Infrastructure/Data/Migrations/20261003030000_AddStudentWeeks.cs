using FocusLens.Infrastructure.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FocusLens.Infrastructure.Data.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20261003030000_AddStudentWeeks")]
public partial class AddStudentWeeks : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "StudentWeeks",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                StudentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                StartsOn = table.Column<DateOnly>(type: "date", nullable: false),
                EndsOn = table.Column<DateOnly>(type: "date", nullable: false),
                CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                LastModifiedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                LastModifiedBy = table.Column<string>(type: "nvarchar(max)", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_StudentWeeks", x => x.Id);
                table.ForeignKey("FK_StudentWeeks_Students_StudentId", x => x.StudentId, "Students", "Id", onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex("IX_StudentWeeks_StudentId_StartsOn", "StudentWeeks", new[] { "StudentId", "StartsOn" }, unique: true);

        // Preserve all represented historical weeks. Session dates are the legacy UTC dates;
        // future/current local calendar weeks are created through StudentLocalTime at runtime.
        migrationBuilder.Sql("""
            ;WITH CandidateWeeks AS (
                SELECT Id AS StudentId, StudyTimeGoalStartDate AS StartsOn
                FROM Students WHERE StudyTimeGoalStartDate IS NOT NULL
                UNION
                SELECT StudentId, StartDate FROM StudyGoalProposals WHERE StartDate IS NOT NULL
                UNION
                SELECT ss.StudentId,
                    DATEADD(day, -((DATEDIFF(day, '19000107', CONVERT(date, ss.StartedAtUtc)) -
                        CASE s.WeekStartsOn
                            WHEN 'Sunday' THEN 0 WHEN 'Monday' THEN 1 WHEN 'Tuesday' THEN 2
                            WHEN 'Wednesday' THEN 3 WHEN 'Thursday' THEN 4 WHEN 'Friday' THEN 5
                            WHEN 'Saturday' THEN 6 ELSE 1 END + 7) % 7), CONVERT(date, ss.StartedAtUtc))
                FROM StudySessions ss INNER JOIN Students s ON s.Id = ss.StudentId
                WHERE ss.StartedAtUtc IS NOT NULL
            )
            INSERT INTO StudentWeeks (Id, StudentId, StartsOn, EndsOn, CreatedAtUtc, CreatedBy, LastModifiedUtc, LastModifiedBy)
            SELECT NEWID(), c.StudentId, c.StartsOn, DATEADD(day, 6, c.StartsOn), SYSUTCDATETIME(), NULL, SYSUTCDATETIME(), NULL
            FROM CandidateWeeks c
            WHERE NOT EXISTS (SELECT 1 FROM StudentWeeks w WHERE w.StudentId = c.StudentId AND w.StartsOn = c.StartsOn)
            GROUP BY c.StudentId, c.StartsOn;
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder) => migrationBuilder.DropTable("StudentWeeks");
}
