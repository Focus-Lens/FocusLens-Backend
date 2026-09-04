using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FocusLens.Infrastructure.Data.Migrations
{
    public partial class AddEmailVerificationCodePurpose : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_EmailVerificationCodes_Email_UsedOnUtc",
                table: "EmailVerificationCodes");

            migrationBuilder.AddColumn<int>(
                name: "Purpose",
                table: "EmailVerificationCodes",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "IX_EmailVerificationCodes_Email_Purpose_UsedOnUtc",
                table: "EmailVerificationCodes",
                columns: new[] { "Email", "Purpose", "UsedOnUtc" });
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_EmailVerificationCodes_Email_Purpose_UsedOnUtc",
                table: "EmailVerificationCodes");

            migrationBuilder.DropColumn(
                name: "Purpose",
                table: "EmailVerificationCodes");

            migrationBuilder.CreateIndex(
                name: "IX_EmailVerificationCodes_Email_UsedOnUtc",
                table: "EmailVerificationCodes",
                columns: new[] { "Email", "UsedOnUtc" });
        }
    }
}
