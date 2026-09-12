using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FocusLens.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddChildSetupDraft : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ChildSetupDrafts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ParentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FirstName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    LastName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    DateOfBirth = table.Column<DateOnly>(type: "date", nullable: true),
                    Grade = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    StudyPriorities = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    StudyTimeGoalPeriod = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    StudyTimeGoalTargetMinutes = table.Column<int>(type: "int", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    LastModifiedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    LastModifiedBy = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChildSetupDrafts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ChildSetupDrafts_Parents_ParentId",
                        column: x => x.ParentId,
                        principalTable: "Parents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ChildSetupSubjects",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Type = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    CustomName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    ChildSetupDraftId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChildSetupSubjects", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ChildSetupSubjects_ChildSetupDrafts_ChildSetupDraftId",
                        column: x => x.ChildSetupDraftId,
                        principalTable: "ChildSetupDrafts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ChildSetupDrafts_ParentId",
                table: "ChildSetupDrafts",
                column: "ParentId");

            migrationBuilder.CreateIndex(
                name: "IX_ChildSetupSubjects_ChildSetupDraftId",
                table: "ChildSetupSubjects",
                column: "ChildSetupDraftId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ChildSetupSubjects");

            migrationBuilder.DropTable(
                name: "ChildSetupDrafts");
        }
    }
}
