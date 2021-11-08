using Microsoft.EntityFrameworkCore.Migrations;

namespace Stock.Trading.Migrations
{
    public partial class AddEventsDealEndingFlagAndIndexes : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsSentToDealEnding",
                table: "OrderEvents",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.CreateIndex(
                name: "IX_OrderEvents_EventType",
                table: "OrderEvents",
                column: "EventType");

            migrationBuilder.CreateIndex(
                name: "IX_OrderEvents_IsSentToDealEnding",
                table: "OrderEvents",
                column: "IsSentToDealEnding");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_OrderEvents_EventType",
                table: "OrderEvents");

            migrationBuilder.DropIndex(
                name: "IX_OrderEvents_IsSentToDealEnding",
                table: "OrderEvents");

            migrationBuilder.DropColumn(
                name: "IsSentToDealEnding",
                table: "OrderEvents");
        }
    }
}
