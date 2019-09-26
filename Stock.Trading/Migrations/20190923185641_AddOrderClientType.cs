using Microsoft.EntityFrameworkCore.Migrations;

namespace Stock.Trading.Migrations
{
    public partial class AddOrderClientType : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "FromInnerTradingBot",
                table: "Bids");

            migrationBuilder.DropColumn(
                name: "FromInnerTradingBot",
                table: "Asks");

            migrationBuilder.AddColumn<int>(
                name: "ClientType",
                table: "Bids",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "ClientType",
                table: "Asks",
                nullable: false,
                defaultValue: 0);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ClientType",
                table: "Bids");

            migrationBuilder.DropColumn(
                name: "ClientType",
                table: "Asks");

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
