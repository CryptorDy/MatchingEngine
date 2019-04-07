using Microsoft.EntityFrameworkCore.Migrations;

namespace Stock.Trading.Migrations
{
    public partial class addFromInnerBotColumns : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "FromInnerTradingBot",
                table: "Deals",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "FromInnerTradingBot",
                table: "Bids",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "FromInnerTradingBot",
                table: "Asks",
                nullable: false,
                defaultValue: false);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "FromInnerTradingBot",
                table: "Deals");

            migrationBuilder.DropColumn(
                name: "FromInnerTradingBot",
                table: "Bids");

            migrationBuilder.DropColumn(
                name: "FromInnerTradingBot",
                table: "Asks");
        }
    }
}
