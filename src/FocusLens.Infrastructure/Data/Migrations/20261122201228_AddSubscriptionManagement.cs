using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FocusLens.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddSubscriptionManagement : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "CancellationApplied",
                table: "PaymentPurchases",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "CancellationRequestedAtUtc",
                table: "PaymentPurchases",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CardBrand",
                table: "PaymentPurchases",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CardLastFour",
                table: "PaymentPurchases",
                type: "nvarchar(4)",
                maxLength: 4,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "KeepStudentId",
                table: "PaymentPurchases",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "BillingContacts",
                columns: table => new
                {
                    BuyerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FullName = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Email = table.Column<string>(type: "nvarchar(254)", maxLength: 254, nullable: false),
                    City = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Country = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: true),
                    Address = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BillingContacts", x => x.BuyerId);
                    table.ForeignKey(
                        name: "FK_BillingContacts_AspNetUsers_BuyerId",
                        column: x => x.BuyerId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PaymentPurchases_BuyerId_IsSandbox_Status_PaidAtUtc",
                table: "PaymentPurchases",
                columns: new[] { "BuyerId", "IsSandbox", "Status", "PaidAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_PaymentPurchases_CancellationApplied_PaidThroughUtc",
                table: "PaymentPurchases",
                columns: new[] { "CancellationApplied", "PaidThroughUtc" },
                filter: "[CancellationRequestedAtUtc] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BillingContacts");

            migrationBuilder.DropIndex(
                name: "IX_PaymentPurchases_BuyerId_IsSandbox_Status_PaidAtUtc",
                table: "PaymentPurchases");

            migrationBuilder.DropIndex(
                name: "IX_PaymentPurchases_CancellationApplied_PaidThroughUtc",
                table: "PaymentPurchases");

            migrationBuilder.DropColumn(
                name: "CancellationApplied",
                table: "PaymentPurchases");

            migrationBuilder.DropColumn(
                name: "CancellationRequestedAtUtc",
                table: "PaymentPurchases");

            migrationBuilder.DropColumn(
                name: "CardBrand",
                table: "PaymentPurchases");

            migrationBuilder.DropColumn(
                name: "CardLastFour",
                table: "PaymentPurchases");

            migrationBuilder.DropColumn(
                name: "KeepStudentId",
                table: "PaymentPurchases");
        }
    }
}
