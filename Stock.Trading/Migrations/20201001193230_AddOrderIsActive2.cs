using Microsoft.EntityFrameworkCore.Migrations;

namespace Stock.Trading.Migrations
{
    public partial class AddOrderIsActive2 : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsActive2",
                table: "Bids",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsActive2",
                table: "Asks",
                nullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsActive2",
                table: "Bids");

            migrationBuilder.DropColumn(
                name: "IsActive2",
                table: "Asks");
        }
    }
}
