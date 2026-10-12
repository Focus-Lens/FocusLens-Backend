using FocusLens.Infrastructure.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FocusLens.Infrastructure.Data.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20261012090000_AddStudySessionType")]
public partial class AddStudySessionType : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "SessionType",
            table: "StudySessions",
            type: "nvarchar(30)",
            maxLength: 30,
            nullable: false,
            defaultValue: "StudySession");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "SessionType",
            table: "StudySessions");
    }
}
