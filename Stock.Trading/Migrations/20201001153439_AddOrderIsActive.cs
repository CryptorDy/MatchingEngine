using Microsoft.EntityFrameworkCore.Migrations;

namespace Stock.Trading.Migrations
{
    public partial class AddOrderIsActive : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsActiveOrder",
                table: "Bids",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsActiveOrder",
                table: "Asks",
                nullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsActiveOrder",
                table: "Bids");

            migrationBuilder.DropColumn(
                name: "IsActiveOrder",
                table: "Asks");
        }
    }
}
