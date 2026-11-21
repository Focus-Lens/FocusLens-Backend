using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FocusLens.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddSandboxPurchases : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SandboxPurchases",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BuyerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Plan = table.Column<int>(type: "int", nullable: false),
                    Term = table.Column<int>(type: "int", nullable: false),
                    Method = table.Column<int>(type: "int", nullable: false),
                    StudentIds = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    AmountPiasters = table.Column<int>(type: "int", nullable: false),
                    IntegrationId = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    ProviderOrderId = table.Column<long>(type: "bigint", nullable: true),
                    PaidTransactionId = table.Column<long>(type: "bigint", nullable: true),
                    CheckoutUrl = table.Column<string>(type: "nvarchar(2048)", maxLength: 2048, nullable: true),
                    PaidAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    PaidThroughUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SandboxPurchases", x => new { x.BuyerId, x.Id });
                    table.ForeignKey(
                        name: "FK_SandboxPurchases_AspNetUsers_BuyerId",
                        column: x => x.BuyerId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SandboxPurchases_PaidTransactionId",
                table: "SandboxPurchases",
                column: "PaidTransactionId",
                unique: true,
                filter: "[PaidTransactionId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_SandboxPurchases_ProviderOrderId",
                table: "SandboxPurchases",
                column: "ProviderOrderId",
                unique: true,
                filter: "[ProviderOrderId] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SandboxPurchases");
        }
    }
}
