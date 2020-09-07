using Microsoft.EntityFrameworkCore.Migrations;

namespace Stock.Trading.Migrations
{
    public partial class AddLiquidityBlocksCount : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "LiquidityBlocksCount",
                table: "OrderEvents",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "LiquidityBlocksCount",
                table: "Bids",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "LiquidityBlocksCount",
                table: "Asks",
                nullable: false,
                defaultValue: 0);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LiquidityBlocksCount",
                table: "OrderEvents");

            migrationBuilder.DropColumn(
                name: "LiquidityBlocksCount",
                table: "Bids");

            migrationBuilder.DropColumn(
                name: "LiquidityBlocksCount",
                table: "Asks");
        }
    }
}
