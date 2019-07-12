using Microsoft.EntityFrameworkCore.Migrations;
using System;

namespace Stock.Trading.Migrations
{
    public partial class AddNewOrdersAndDealsEntities : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AsksV2",
                columns: table => new
                {
                    Id = table.Column<Guid>(nullable: false),
                    IsBid = table.Column<bool>(nullable: false),
                    Price = table.Column<decimal>(nullable: false),
                    Amount = table.Column<decimal>(nullable: false),
                    Fulfilled = table.Column<decimal>(nullable: false),
                    Blocked = table.Column<decimal>(nullable: false),
                    CurrencyPairCode = table.Column<string>(nullable: false),
                    DateCreated = table.Column<DateTimeOffset>(nullable: false),
                    UserId = table.Column<string>(nullable: false),
                    IsCanceled = table.Column<bool>(nullable: false),
                    Exchange = table.Column<int>(nullable: false),
                    FromInnerTradingBot = table.Column<bool>(nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AsksV2", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "BidsV2",
                columns: table => new
                {
                    Id = table.Column<Guid>(nullable: false),
                    IsBid = table.Column<bool>(nullable: false),
                    Price = table.Column<decimal>(nullable: false),
                    Amount = table.Column<decimal>(nullable: false),
                    Fulfilled = table.Column<decimal>(nullable: false),
                    Blocked = table.Column<decimal>(nullable: false),
                    CurrencyPairCode = table.Column<string>(nullable: false),
                    DateCreated = table.Column<DateTimeOffset>(nullable: false),
                    UserId = table.Column<string>(nullable: false),
                    IsCanceled = table.Column<bool>(nullable: false),
                    Exchange = table.Column<int>(nullable: false),
                    FromInnerTradingBot = table.Column<bool>(nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BidsV2", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DealsV2",
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
                    table.PrimaryKey("PK_DealsV2", x => x.DealId);
                    table.ForeignKey(
                        name: "FK_DealsV2_AsksV2_AskId",
                        column: x => x.AskId,
                        principalTable: "AsksV2",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_DealsV2_BidsV2_BidId",
                        column: x => x.BidId,
                        principalTable: "BidsV2",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DealsV2_AskId",
                table: "DealsV2",
                column: "AskId");

            migrationBuilder.CreateIndex(
                name: "IX_DealsV2_BidId",
                table: "DealsV2",
                column: "BidId");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DealsV2");

            migrationBuilder.DropTable(
                name: "AsksV2");

            migrationBuilder.DropTable(
                name: "BidsV2");
        }
    }
}
