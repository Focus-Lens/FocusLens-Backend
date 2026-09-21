using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FocusLens.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddChildSetupInvitationTypes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "TargetEmailNormalized",
                table: "ChildSetupInvitations",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(256)",
                oldMaxLength: 256);

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "ChildSetupInvitations",
                type: "rowversion",
                rowVersion: true,
                nullable: false,
                defaultValue: new byte[0]);

            migrationBuilder.AddColumn<string>(
                name: "Type",
                table: "ChildSetupInvitations",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Email");

            migrationBuilder.AddCheckConstraint(
                name: "CK_ChildSetupInvitations_Type_TargetEmail",
                table: "ChildSetupInvitations",
                sql: "([Type] = 'Email' AND [TargetEmailNormalized] IS NOT NULL) OR ([Type] = 'Link' AND [TargetEmailNormalized] IS NULL)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_ChildSetupInvitations_Type_TargetEmail",
                table: "ChildSetupInvitations");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "ChildSetupInvitations");

            migrationBuilder.DropColumn(
                name: "Type",
                table: "ChildSetupInvitations");

            migrationBuilder.AlterColumn<string>(
                name: "TargetEmailNormalized",
                table: "ChildSetupInvitations",
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
