using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FocusLens.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddApplicationUserCreationTimestamp : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "CreatedAtUtc",
                table: "AspNetUsers",
                type: "datetimeoffset",
                nullable: false,
                defaultValueSql: "SYSUTCDATETIME()");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CreatedAtUtc",
                table: "AspNetUsers");
        }
    }
}
