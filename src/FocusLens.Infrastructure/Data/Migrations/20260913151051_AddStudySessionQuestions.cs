using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FocusLens.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddStudySessionQuestions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AiExtractedText",
                table: "StudySessionSelections",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "StudySessionQuestions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    StudySessionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    StudyMaterialSectionId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    AiSectionId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    AiSectionTitle = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    AiConceptId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    AiConceptName = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    Question = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    CorrectAnswer = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    Difficulty = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Explanation = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                    Order = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StudySessionQuestions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StudySessionQuestions_StudyMaterialSections_StudyMaterialSectionId",
                        column: x => x.StudyMaterialSectionId,
                        principalTable: "StudyMaterialSections",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StudySessionQuestions_StudySessions_StudySessionId",
                        column: x => x.StudySessionId,
                        principalTable: "StudySessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "StudySessionQuestionAnswers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    StudySessionQuestionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SelectedAnswer = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    IsCorrect = table.Column<bool>(type: "bit", nullable: false),
                    LearningSignal = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    AttemptNumber = table.Column<int>(type: "int", nullable: false),
                    AnsweredAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StudySessionQuestionAnswers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StudySessionQuestionAnswers_StudySessionQuestions_StudySessionQuestionId",
                        column: x => x.StudySessionQuestionId,
                        principalTable: "StudySessionQuestions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "StudySessionQuestionOptions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    StudySessionQuestionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Text = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    Order = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StudySessionQuestionOptions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StudySessionQuestionOptions_StudySessionQuestions_StudySessionQuestionId",
                        column: x => x.StudySessionQuestionId,
                        principalTable: "StudySessionQuestions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_StudySessionQuestionAnswers_StudySessionQuestionId_AttemptNumber",
                table: "StudySessionQuestionAnswers",
                columns: new[] { "StudySessionQuestionId", "AttemptNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_StudySessionQuestionOptions_StudySessionQuestionId_Order",
                table: "StudySessionQuestionOptions",
                columns: new[] { "StudySessionQuestionId", "Order" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_StudySessionQuestions_StudyMaterialSectionId",
                table: "StudySessionQuestions",
                column: "StudyMaterialSectionId");

            migrationBuilder.CreateIndex(
                name: "IX_StudySessionQuestions_StudySessionId_Order",
                table: "StudySessionQuestions",
                columns: new[] { "StudySessionId", "Order" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "StudySessionQuestionAnswers");

            migrationBuilder.DropTable(
                name: "StudySessionQuestionOptions");

            migrationBuilder.DropTable(
                name: "StudySessionQuestions");

            migrationBuilder.DropColumn(
                name: "AiExtractedText",
                table: "StudySessionSelections");
        }
    }
}
