using System;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Stock.Trading.Migrations
{
    public partial class AddOrderEvents : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "OrderEvents",
                columns: table => new
                {
                    EventId = table.Column<Guid>(nullable: false),
                    EventDate = table.Column<DateTimeOffset>(nullable: false, defaultValueSql: "current_timestamp"),
                    EventType = table.Column<string>(nullable: false),
                    EventDealIds = table.Column<string>(nullable: true),
                    IsSavedInMarketData = table.Column<bool>(nullable: false),
                    Id = table.Column<Guid>(nullable: false),
                    IsBid = table.Column<bool>(nullable: false),
                    Price = table.Column<decimal>(nullable: false),
                    Amount = table.Column<decimal>(nullable: false),
                    Fulfilled = table.Column<decimal>(nullable: false),
                    Blocked = table.Column<decimal>(nullable: false),
                    CurrencyPairCode = table.Column<string>(nullable: false),
                    DateCreated = table.Column<DateTimeOffset>(nullable: false),
                    ClientType = table.Column<int>(nullable: false),
                    UserId = table.Column<string>(nullable: false),
                    IsCanceled = table.Column<bool>(nullable: false),
                    Exchange = table.Column<int>(nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrderEvents", x => x.EventId);
                });
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "OrderEvents");
        }
    }
}
