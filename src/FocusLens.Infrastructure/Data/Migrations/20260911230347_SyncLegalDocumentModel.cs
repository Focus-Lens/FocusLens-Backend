using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FocusLens.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class SyncLegalDocumentModel : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "LegalDocuments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Audience = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Version = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Content = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    IsPublished = table.Column<bool>(type: "bit", nullable: false),
                    PublishedOnUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    LastModifiedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    LastModifiedBy = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LegalDocuments", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "UserTermsAcceptances",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LegalDocumentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AcceptedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserTermsAcceptances", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserTermsAcceptances_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_UserTermsAcceptances_LegalDocuments_LegalDocumentId",
                        column: x => x.LegalDocumentId,
                        principalTable: "LegalDocuments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.InsertData(
                table: "LegalDocuments",
                columns: new[]
                {
                    "Id",
                    "Audience",
                    "Version",
                    "Content",
                    "IsPublished",
                    "PublishedOnUtc",
                    "CreatedAtUtc",
                    "CreatedBy",
                    "LastModifiedUtc",
                    "LastModifiedBy"
                },
                values: new object[,]
                {
                    {
                        new Guid("11111111-1111-7111-8111-111111111111"),
                        "Student",
                        "2026-09-04",
                        "FocusLens Student Terms & Privacy\n\nFocusLens uses account, profile, and learning preference information to provide the student application experience, secure user access, and support learning workflows.",
                        true,
                        new DateTimeOffset(new DateTime(2026, 9, 4, 0, 0, 0, DateTimeKind.Unspecified), TimeSpan.Zero),
                        new DateTimeOffset(new DateTime(2026, 9, 4, 0, 0, 0, DateTimeKind.Unspecified), TimeSpan.Zero),
                        null,
                        new DateTimeOffset(new DateTime(2026, 9, 4, 0, 0, 0, DateTimeKind.Unspecified), TimeSpan.Zero),
                        null
                    },
                    {
                        new Guid("22222222-2222-7222-8222-222222222222"),
                        "Parent",
                        "2026-09-04",
                        "FocusLens Parent Terms & Privacy\n\nFocusLens uses account and relationship information to provide the parent application experience, secure user access, and support parent/student relationships.",
                        true,
                        new DateTimeOffset(new DateTime(2026, 9, 4, 0, 0, 0, DateTimeKind.Unspecified), TimeSpan.Zero),
                        new DateTimeOffset(new DateTime(2026, 9, 4, 0, 0, 0, DateTimeKind.Unspecified), TimeSpan.Zero),
                        null,
                        new DateTimeOffset(new DateTime(2026, 9, 4, 0, 0, 0, DateTimeKind.Unspecified), TimeSpan.Zero),
                        null
                    }
                });

            migrationBuilder.CreateIndex(
                name: "IX_LegalDocuments_Audience_IsPublished_PublishedOnUtc",
                table: "LegalDocuments",
                columns: new[] { "Audience", "IsPublished", "PublishedOnUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_LegalDocuments_Audience_Version",
                table: "LegalDocuments",
                columns: new[] { "Audience", "Version" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UserTermsAcceptances_LegalDocumentId",
                table: "UserTermsAcceptances",
                column: "LegalDocumentId");

            migrationBuilder.CreateIndex(
                name: "IX_UserTermsAcceptances_UserId_LegalDocumentId",
                table: "UserTermsAcceptances",
                columns: new[] { "UserId", "LegalDocumentId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "UserTermsAcceptances");

            migrationBuilder.DropTable(
                name: "LegalDocuments");
        }
    }
}
