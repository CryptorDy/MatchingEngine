using Microsoft.EntityFrameworkCore.Migrations;

namespace Stock.Trading.Migrations
{
    public partial class addExchangeId : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ExchangeId",
                table: "Bids",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "ExchangeId",
                table: "Asks",
                nullable: false,
                defaultValue: 0);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ExchangeId",
                table: "Bids");

            migrationBuilder.DropColumn(
                name: "ExchangeId",
                table: "Asks");
        }
    }
}
