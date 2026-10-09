using FocusLens.Infrastructure.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FocusLens.Infrastructure.Data.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20261009010000_AddBehaviorWindowActiveTimeSeconds")]
public partial class AddBehaviorWindowActiveTimeSeconds : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<double>(
            name: "WindowActiveTimeSeconds",
            table: "StudySessionBehaviorWindows",
            type: "float",
            nullable: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "WindowActiveTimeSeconds",
            table: "StudySessionBehaviorWindows");
    }
}
