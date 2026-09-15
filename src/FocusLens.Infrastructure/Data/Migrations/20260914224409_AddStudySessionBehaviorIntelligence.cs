using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FocusLens.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddStudySessionBehaviorIntelligence : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "StudySessionBehaviorEvents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    StudySessionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    StudyMaterialSectionId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    StudySessionQuestionId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    EventType = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    OccurredAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ScrollSpeedAvgPxPerSec = table.Column<double>(type: "float", nullable: true),
                    ScrollDirectionChanges = table.Column<int>(type: "int", nullable: true),
                    ContentProgressionPct = table.Column<double>(type: "float", nullable: true),
                    InteractionCount = table.Column<int>(type: "int", nullable: true),
                    BackgroundCount = table.Column<int>(type: "int", nullable: true),
                    BackgroundDurationSeconds = table.Column<double>(type: "float", nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    LastModifiedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    LastModifiedBy = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StudySessionBehaviorEvents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StudySessionBehaviorEvents_StudyMaterialSections_StudyMaterialSectionId",
                        column: x => x.StudyMaterialSectionId,
                        principalTable: "StudyMaterialSections",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_StudySessionBehaviorEvents_StudySessionQuestions_StudySessionQuestionId",
                        column: x => x.StudySessionQuestionId,
                        principalTable: "StudySessionQuestions",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_StudySessionBehaviorEvents_StudySessions_StudySessionId",
                        column: x => x.StudySessionId,
                        principalTable: "StudySessions",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "StudySessionBehaviorWindows",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    StudySessionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    WindowIndex = table.Column<int>(type: "int", nullable: false),
                    WindowStartUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    WindowEndUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    IsFinal = table.Column<bool>(type: "bit", nullable: false),
                    FocusScore = table.Column<int>(type: "int", nullable: true),
                    FocusState = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    FocusTrend = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    UnderstandingScore = table.Column<int>(type: "int", nullable: true),
                    UnderstandingTrend = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    RawAction = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    RecommendedAction = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    ActionEmitted = table.Column<bool>(type: "bit", nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    LastModifiedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    LastModifiedBy = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StudySessionBehaviorWindows", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StudySessionBehaviorWindows_StudySessions_StudySessionId",
                        column: x => x.StudySessionId,
                        principalTable: "StudySessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_StudySessionBehaviorEvents_StudyMaterialSectionId",
                table: "StudySessionBehaviorEvents",
                column: "StudyMaterialSectionId");

            migrationBuilder.CreateIndex(
                name: "IX_StudySessionBehaviorEvents_StudySessionId_OccurredAtUtc",
                table: "StudySessionBehaviorEvents",
                columns: new[] { "StudySessionId", "OccurredAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_StudySessionBehaviorEvents_StudySessionQuestionId",
                table: "StudySessionBehaviorEvents",
                column: "StudySessionQuestionId");

            migrationBuilder.CreateIndex(
                name: "IX_StudySessionBehaviorWindows_StudySessionId_WindowIndex",
                table: "StudySessionBehaviorWindows",
                columns: new[] { "StudySessionId", "WindowIndex" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "StudySessionBehaviorEvents");

            migrationBuilder.DropTable(
                name: "StudySessionBehaviorWindows");
        }
    }
}
