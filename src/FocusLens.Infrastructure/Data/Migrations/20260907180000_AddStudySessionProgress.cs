using FocusLens.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FocusLens.Infrastructure.Data.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20260907180000_AddStudySessionProgress")]
public partial class AddStudySessionProgress : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<int>(
            name: "CurrentPage",
            table: "StudySessions",
            type: "int",
            nullable: true);

        migrationBuilder.AddColumn<DateTimeOffset>(
            name: "LastActivityAtUtc",
            table: "StudySessions",
            type: "datetimeoffset",
            nullable: true);

        migrationBuilder.CreateTable(
            name: "StudySessionCompletedSections",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                StudySessionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                StudyMaterialSectionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_StudySessionCompletedSections", x => x.Id);
                table.ForeignKey(
                    name: "FK_StudySessionCompletedSections_StudyMaterialSections_StudyMaterialSectionId",
                    column: x => x.StudyMaterialSectionId,
                    principalTable: "StudyMaterialSections",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_StudySessionCompletedSections_StudySessions_StudySessionId",
                    column: x => x.StudySessionId,
                    principalTable: "StudySessions",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "IX_StudySessionCompletedSections_StudyMaterialSectionId",
            table: "StudySessionCompletedSections",
            column: "StudyMaterialSectionId");

        migrationBuilder.CreateIndex(
            name: "IX_StudySessionCompletedSections_StudySessionId_StudyMaterialSectionId",
            table: "StudySessionCompletedSections",
            columns: new[] { "StudySessionId", "StudyMaterialSectionId" },
            unique: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "StudySessionCompletedSections");
        migrationBuilder.DropColumn(name: "CurrentPage", table: "StudySessions");
        migrationBuilder.DropColumn(name: "LastActivityAtUtc", table: "StudySessions");
    }
}
