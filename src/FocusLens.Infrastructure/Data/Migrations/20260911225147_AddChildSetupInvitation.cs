using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FocusLens.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddChildSetupInvitation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "ClaimedByStudentId",
                table: "ChildSetupDrafts",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ChildSetupInvitations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ChildSetupDraftId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TargetEmailNormalized = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    TokenHash = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    ExpiresAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    ClaimedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    LastModifiedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    LastModifiedBy = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChildSetupInvitations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ChildSetupInvitations_ChildSetupDrafts_ChildSetupDraftId",
                        column: x => x.ChildSetupDraftId,
                        principalTable: "ChildSetupDrafts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ChildSetupDrafts_ClaimedByStudentId",
                table: "ChildSetupDrafts",
                column: "ClaimedByStudentId");

            migrationBuilder.CreateIndex(
                name: "IX_ChildSetupInvitations_ChildSetupDraftId_TargetEmailNormalized_Status",
                table: "ChildSetupInvitations",
                columns: new[] { "ChildSetupDraftId", "TargetEmailNormalized", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_ChildSetupInvitations_TokenHash",
                table: "ChildSetupInvitations",
                column: "TokenHash",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_ChildSetupDrafts_Students_ClaimedByStudentId",
                table: "ChildSetupDrafts",
                column: "ClaimedByStudentId",
                principalTable: "Students",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ChildSetupDrafts_Students_ClaimedByStudentId",
                table: "ChildSetupDrafts");

            migrationBuilder.DropTable(
                name: "ChildSetupInvitations");

            migrationBuilder.DropIndex(
                name: "IX_ChildSetupDrafts_ClaimedByStudentId",
                table: "ChildSetupDrafts");

            migrationBuilder.DropColumn(
                name: "ClaimedByStudentId",
                table: "ChildSetupDrafts");
        }
    }
}
