using Microsoft.EntityFrameworkCore.Migrations;

namespace Stock.Trading.Migrations
{
    public partial class RemoveMatchingExternalTradeRelationToOrders : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ExternalTrades_Asks_AskId",
                table: "ExternalTrades");

            migrationBuilder.DropForeignKey(
                name: "FK_ExternalTrades_Bids_BidId",
                table: "ExternalTrades");

            migrationBuilder.DropIndex(
                name: "IX_ExternalTrades_AskId",
                table: "ExternalTrades");

            migrationBuilder.DropIndex(
                name: "IX_ExternalTrades_BidId",
                table: "ExternalTrades");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_ExternalTrades_AskId",
                table: "ExternalTrades",
                column: "AskId");

            migrationBuilder.CreateIndex(
                name: "IX_ExternalTrades_BidId",
                table: "ExternalTrades",
                column: "BidId");

            migrationBuilder.AddForeignKey(
                name: "FK_ExternalTrades_Asks_AskId",
                table: "ExternalTrades",
                column: "AskId",
                principalTable: "Asks",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_ExternalTrades_Bids_BidId",
                table: "ExternalTrades",
                column: "BidId",
                principalTable: "Bids",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
