using Microsoft.EntityFrameworkCore.Migrations;

namespace Stock.Trading.Migrations
{
    public partial class AddDealIndexes : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_Deals_FromInnerTradingBot",
                table: "Deals",
                column: "FromInnerTradingBot");

            migrationBuilder.CreateIndex(
                name: "IX_Deals_IsSentToDealEnding",
                table: "Deals",
                column: "IsSentToDealEnding");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Deals_FromInnerTradingBot",
                table: "Deals");

            migrationBuilder.DropIndex(
                name: "IX_Deals_IsSentToDealEnding",
                table: "Deals");
        }
    }
}
