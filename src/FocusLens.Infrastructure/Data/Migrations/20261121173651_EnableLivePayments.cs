using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FocusLens.Infrastructure.Data.Migrations
{
    public partial class EnableLivePayments : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey("FK_SandboxPurchases_AspNetUsers_BuyerId", "SandboxPurchases");
            migrationBuilder.DropPrimaryKey("PK_SandboxPurchases", "SandboxPurchases");
            migrationBuilder.DropIndex("IX_SandboxPurchases_PaidTransactionId", "SandboxPurchases");
            migrationBuilder.DropIndex("IX_SandboxPurchases_ProviderOrderId", "SandboxPurchases");
            migrationBuilder.RenameTable(name: "SandboxPurchases", newName: "PaymentPurchases");

            migrationBuilder.AddColumn<bool>(
                name: "IsSandbox", table: "PaymentPurchases", type: "bit", nullable: false, defaultValue: true);

            migrationBuilder.AddPrimaryKey("PK_PaymentPurchases", "PaymentPurchases", ["BuyerId", "Id", "IsSandbox"]);
            migrationBuilder.AddForeignKey(
                name: "FK_PaymentPurchases_AspNetUsers_BuyerId",
                table: "PaymentPurchases",
                column: "BuyerId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.CreateIndex(
                name: "IX_PaymentPurchases_IsSandbox_PaidTransactionId",
                table: "PaymentPurchases",
                columns: ["IsSandbox", "PaidTransactionId"],
                unique: true,
                filter: "[PaidTransactionId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_PaymentPurchases_IsSandbox_ProviderOrderId",
                table: "PaymentPurchases",
                columns: ["IsSandbox", "ProviderOrderId"],
                unique: true,
                filter: "[ProviderOrderId] IS NOT NULL");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                IF EXISTS (SELECT 1 FROM [PaymentPurchases] WHERE [IsSandbox] = 0)
                    THROW 51000, 'Cannot roll back while live payment records exist.', 1;
                """);

            migrationBuilder.DropForeignKey("FK_PaymentPurchases_AspNetUsers_BuyerId", "PaymentPurchases");
            migrationBuilder.DropPrimaryKey("PK_PaymentPurchases", "PaymentPurchases");
            migrationBuilder.DropIndex("IX_PaymentPurchases_IsSandbox_PaidTransactionId", "PaymentPurchases");
            migrationBuilder.DropIndex("IX_PaymentPurchases_IsSandbox_ProviderOrderId", "PaymentPurchases");
            migrationBuilder.DropColumn("IsSandbox", "PaymentPurchases");
            migrationBuilder.RenameTable(name: "PaymentPurchases", newName: "SandboxPurchases");

            migrationBuilder.AddPrimaryKey("PK_SandboxPurchases", "SandboxPurchases", ["BuyerId", "Id"]);
            migrationBuilder.AddForeignKey(
                name: "FK_SandboxPurchases_AspNetUsers_BuyerId",
                table: "SandboxPurchases",
                column: "BuyerId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

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
    }
}
