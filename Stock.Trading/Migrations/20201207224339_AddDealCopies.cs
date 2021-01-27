using System;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Stock.Trading.Migrations
{
    public partial class AddDealCopies : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DealCopies",
                columns: table => new
                {
                    DealId = table.Column<Guid>(nullable: false),
                    DateCreated = table.Column<DateTimeOffset>(nullable: false),
                    Volume = table.Column<decimal>(nullable: false),
                    Price = table.Column<decimal>(nullable: false),
                    FromInnerTradingBot = table.Column<bool>(nullable: false),
                    AskId = table.Column<Guid>(nullable: false),
                    BidId = table.Column<Guid>(nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DealCopies", x => x.DealId);
                });
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DealCopies");
        }
    }
}
