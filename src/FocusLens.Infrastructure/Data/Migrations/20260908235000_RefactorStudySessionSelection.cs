using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FocusLens.Infrastructure.Data.Migrations;

public partial class RefactorStudySessionSelection : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropForeignKey(
            name: "FK_StudySessionSelectedSections_StudySessions_StudySessionId",
            table: "StudySessionSelectedSections");

        migrationBuilder.DropIndex(
            name: "IX_StudySessionSelectedSections_StudySessionId_StudyMaterialSectionId",
            table: "StudySessionSelectedSections");

        migrationBuilder.DropIndex(
            name: "IX_StudySessions_StudyMaterialId",
            table: "StudySessions");

        migrationBuilder.AddColumn<Guid>(
            name: "StudySessionSelectionId",
            table: "StudySessionSelectedSections",
            type: "uniqueidentifier",
            nullable: true);

        migrationBuilder.CreateTable(
            name: "StudySessionSelections",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                StudySessionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                StudyMaterialId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                FromPage = table.Column<int>(type: "int", nullable: false),
                ToPage = table.Column<int>(type: "int", nullable: false),
                DerivedStorageReference = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                LastModifiedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                LastModifiedBy = table.Column<string>(type: "nvarchar(max)", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_StudySessionSelections", x => x.Id);
                table.ForeignKey(
                    name: "FK_StudySessionSelections_StudyMaterials_StudyMaterialId",
                    column: x => x.StudyMaterialId,
                    principalTable: "StudyMaterials",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.Sql("""
            INSERT INTO StudySessionSelections
                (Id, StudySessionId, StudyMaterialId, FromPage, ToPage, DerivedStorageReference, CreatedAtUtc, CreatedBy, LastModifiedUtc, LastModifiedBy)
            SELECT NEWID(), Id, StudyMaterialId, FromPage, ToPage, DerivedStorageReference, CreatedAtUtc, CreatedBy, LastModifiedUtc, LastModifiedBy
            FROM StudySessions
            WHERE StudyMaterialId IS NOT NULL AND FromPage IS NOT NULL AND ToPage IS NOT NULL;
            """);

        migrationBuilder.Sql("""
            UPDATE selectedSections
            SET StudySessionSelectionId = selections.Id
            FROM StudySessionSelectedSections AS selectedSections
            INNER JOIN StudySessionSelections AS selections ON selections.StudySessionId = selectedSections.StudySessionId;
            """);

        migrationBuilder.Sql("DELETE FROM StudySessionSelectedSections WHERE StudySessionSelectionId IS NULL;");

        migrationBuilder.DropColumn(name: "StudySessionId", table: "StudySessionSelectedSections");
        migrationBuilder.DropColumn(name: "FromPage", table: "StudySessions");
        migrationBuilder.DropColumn(name: "ToPage", table: "StudySessions");
        migrationBuilder.DropColumn(name: "DerivedStorageReference", table: "StudyMaterials");

        migrationBuilder.AlterColumn<Guid>(
            name: "StudySessionSelectionId",
            table: "StudySessionSelectedSections",
            type: "uniqueidentifier",
            nullable: false,
            oldClrType: typeof(Guid),
            oldType: "uniqueidentifier",
            oldNullable: true);

        migrationBuilder.CreateIndex(
            name: "IX_StudySessionSelections_StudyMaterialId",
            table: "StudySessionSelections",
            column: "StudyMaterialId");

        migrationBuilder.CreateIndex(
            name: "IX_StudySessionSelections_StudySessionId",
            table: "StudySessionSelections",
            column: "StudySessionId",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_StudySessionSelectedSections_StudySessionSelectionId_StudyMaterialSectionId",
            table: "StudySessionSelectedSections",
            columns: new[] { "StudySessionSelectionId", "StudyMaterialSectionId" },
            unique: true);

        migrationBuilder.AddForeignKey(
            name: "FK_StudySessionSelections_StudySessions_StudySessionId",
            table: "StudySessionSelections",
            column: "StudySessionId",
            principalTable: "StudySessions",
            principalColumn: "Id",
            onDelete: ReferentialAction.Cascade);

        migrationBuilder.AddForeignKey(
            name: "FK_StudySessionSelectedSections_StudySessionSelections_StudySessionSelectionId",
            table: "StudySessionSelectedSections",
            column: "StudySessionSelectionId",
            principalTable: "StudySessionSelections",
            principalColumn: "Id",
            onDelete: ReferentialAction.Cascade);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<int>(name: "FromPage", table: "StudySessions", type: "int", nullable: true);
        migrationBuilder.AddColumn<int>(name: "ToPage", table: "StudySessions", type: "int", nullable: true);
        migrationBuilder.AddColumn<string>(name: "DerivedStorageReference", table: "StudyMaterials", type: "nvarchar(500)", maxLength: 500, nullable: true);
        migrationBuilder.AddColumn<Guid>(name: "StudySessionId", table: "StudySessionSelectedSections", type: "uniqueidentifier", nullable: true);

        migrationBuilder.Sql("""
            UPDATE session
            SET FromPage = selection.FromPage,
                ToPage = selection.ToPage
            FROM StudySessions AS session
            INNER JOIN StudySessionSelections AS selection ON selection.StudySessionId = session.Id;

            UPDATE material
            SET DerivedStorageReference = selection.DerivedStorageReference
            FROM StudyMaterials AS material
            INNER JOIN StudySessionSelections AS selection ON selection.StudyMaterialId = material.Id;

            UPDATE selectedSections
            SET StudySessionId = selection.StudySessionId
            FROM StudySessionSelectedSections AS selectedSections
            INNER JOIN StudySessionSelections AS selection ON selection.Id = selectedSections.StudySessionSelectionId;
            """);

        migrationBuilder.DropForeignKey(name: "FK_StudySessionSelectedSections_StudySessionSelections_StudySessionSelectionId", table: "StudySessionSelectedSections");
        migrationBuilder.DropForeignKey(name: "FK_StudySessionSelections_StudyMaterials_StudyMaterialId", table: "StudySessionSelections");
        migrationBuilder.DropForeignKey(name: "FK_StudySessionSelections_StudySessions_StudySessionId", table: "StudySessionSelections");
        migrationBuilder.DropIndex(name: "IX_StudySessionSelectedSections_StudySessionSelectionId_StudyMaterialSectionId", table: "StudySessionSelectedSections");
        migrationBuilder.DropIndex(name: "IX_StudySessionSelections_StudyMaterialId", table: "StudySessionSelections");
        migrationBuilder.DropIndex(name: "IX_StudySessionSelections_StudySessionId", table: "StudySessionSelections");
        migrationBuilder.DropColumn(name: "StudySessionSelectionId", table: "StudySessionSelectedSections");
        migrationBuilder.DropTable(name: "StudySessionSelections");

        migrationBuilder.AlterColumn<Guid>(name: "StudySessionId", table: "StudySessionSelectedSections", type: "uniqueidentifier", nullable: false, oldClrType: typeof(Guid), oldType: "uniqueidentifier", oldNullable: true);
        migrationBuilder.CreateIndex(name: "IX_StudySessions_StudyMaterialId", table: "StudySessions", column: "StudyMaterialId");
        migrationBuilder.CreateIndex(name: "IX_StudySessionSelectedSections_StudySessionId_StudyMaterialSectionId", table: "StudySessionSelectedSections", columns: new[] { "StudySessionId", "StudyMaterialSectionId" }, unique: true);
        migrationBuilder.AddForeignKey(name: "FK_StudySessionSelectedSections_StudySessions_StudySessionId", table: "StudySessionSelectedSections", column: "StudySessionId", principalTable: "StudySessions", principalColumn: "Id", onDelete: ReferentialAction.Cascade);
    }
}
