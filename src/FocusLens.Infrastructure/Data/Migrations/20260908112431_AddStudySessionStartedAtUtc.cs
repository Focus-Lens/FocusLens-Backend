using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FocusLens.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddStudySessionStartedAtUtc : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                IF COL_LENGTH(N'[StudySessions]', N'StartedAtUtc') IS NULL
                BEGIN
                    ALTER TABLE [StudySessions] ADD [StartedAtUtc] datetimeoffset NULL;
                END
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                IF COL_LENGTH(N'[StudySessions]', N'StartedAtUtc') IS NOT NULL
                BEGIN
                    ALTER TABLE [StudySessions] DROP COLUMN [StartedAtUtc];
                END
                """);
        }
    }
}
