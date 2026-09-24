using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FocusLens.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddStudentParentInvitationTypes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "TargetEmailNormalized",
                table: "StudentParentInvitations",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(256)",
                oldMaxLength: 256);

            migrationBuilder.AddColumn<string>(
                name: "Type",
                table: "StudentParentInvitations",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Email");

            migrationBuilder.AddCheckConstraint(
                name: "CK_StudentParentInvitations_Type_TargetEmail",
                table: "StudentParentInvitations",
                sql: "([Type] = 'Email' AND [TargetEmailNormalized] IS NOT NULL) OR ([Type] = 'Link' AND [TargetEmailNormalized] IS NULL)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_StudentParentInvitations_Type_TargetEmail",
                table: "StudentParentInvitations");

            migrationBuilder.DropColumn(
                name: "Type",
                table: "StudentParentInvitations");

            migrationBuilder.AlterColumn<string>(
                name: "TargetEmailNormalized",
                table: "StudentParentInvitations",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "nvarchar(256)",
                oldMaxLength: 256,
                oldNullable: true);
        }
    }
}
