using Microsoft.EntityFrameworkCore.Migrations;

namespace Stock.Trading.Migrations
{
    public partial class RemoveOrderFromInnerTradingBot : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "FromInnerTradingBot",
                table: "Bids");

            migrationBuilder.DropColumn(
                name: "FromInnerTradingBot",
                table: "Asks");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
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
    }
}
