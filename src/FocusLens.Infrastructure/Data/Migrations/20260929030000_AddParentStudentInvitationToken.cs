using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FocusLens.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddParentStudentInvitationToken : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "InvitationTokenHash",
                table: "ParentStudentRelationships",
                type: "nvarchar(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_ParentStudentRelationships_InvitationTokenHash",
                table: "ParentStudentRelationships",
                column: "InvitationTokenHash",
                unique: true,
                filter: "[InvitationTokenHash] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ParentStudentRelationships_InvitationTokenHash",
                table: "ParentStudentRelationships");

            migrationBuilder.DropColumn(
                name: "InvitationTokenHash",
                table: "ParentStudentRelationships");
        }
    }
}
