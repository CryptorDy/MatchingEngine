using System;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Stock.Trading.Migrations
{
    public partial class RemoveOldEntities : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Deals");

            migrationBuilder.DropTable(
                name: "Asks");

            migrationBuilder.DropTable(
                name: "Bids");

            migrationBuilder.DropTable(
                name: "OrderTypes");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "OrderTypes",
                columns: table => new
                {
                    Code = table.Column<string>(nullable: false),
                    Name = table.Column<string>(nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrderTypes", x => x.Code);
                });

            migrationBuilder.CreateTable(
                name: "Asks",
                columns: table => new
                {
                    Id = table.Column<Guid>(nullable: false),
                    CurrencyPairId = table.Column<string>(nullable: false),
                    ExchangeId = table.Column<int>(nullable: false),
                    FromInnerTradingBot = table.Column<bool>(nullable: false),
                    Fulfilled = table.Column<decimal>(nullable: false),
                    OrderDateUtc = table.Column<DateTime>(nullable: false),
                    OrderTypeCode = table.Column<string>(nullable: false),
                    Price = table.Column<decimal>(nullable: false),
                    UserId = table.Column<string>(nullable: false),
                    Volume = table.Column<decimal>(nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Asks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Asks_OrderTypes_OrderTypeCode",
                        column: x => x.OrderTypeCode,
                        principalTable: "OrderTypes",
                        principalColumn: "Code",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Bids",
                columns: table => new
                {
                    Id = table.Column<Guid>(nullable: false),
                    CurrencyPairId = table.Column<string>(nullable: false),
                    ExchangeId = table.Column<int>(nullable: false),
                    FromInnerTradingBot = table.Column<bool>(nullable: false),
                    Fulfilled = table.Column<decimal>(nullable: false),
                    OrderDateUtc = table.Column<DateTime>(nullable: false),
                    OrderTypeCode = table.Column<string>(nullable: false),
                    Price = table.Column<decimal>(nullable: false),
                    UserId = table.Column<string>(nullable: false),
                    Volume = table.Column<decimal>(nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Bids", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Bids_OrderTypes_OrderTypeCode",
                        column: x => x.OrderTypeCode,
                        principalTable: "OrderTypes",
                        principalColumn: "Code",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Deals",
                columns: table => new
                {
                    DealId = table.Column<Guid>(nullable: false),
                    AskId = table.Column<Guid>(nullable: false),
                    BidId = table.Column<Guid>(nullable: false),
                    DealDateUtc = table.Column<DateTime>(nullable: false),
                    FromInnerTradingBot = table.Column<bool>(nullable: false),
                    Price = table.Column<decimal>(nullable: false),
                    Volume = table.Column<decimal>(nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Deals", x => x.DealId);
                    table.ForeignKey(
                        name: "FK_Deals_Asks_AskId",
                        column: x => x.AskId,
                        principalTable: "Asks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Deals_Bids_BidId",
                        column: x => x.BidId,
                        principalTable: "Bids",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Asks_OrderTypeCode",
                table: "Asks",
                column: "OrderTypeCode");

            migrationBuilder.CreateIndex(
                name: "IX_Bids_OrderTypeCode",
                table: "Bids",
                column: "OrderTypeCode");

            migrationBuilder.CreateIndex(
                name: "IX_Deals_AskId",
                table: "Deals",
                column: "AskId");

            migrationBuilder.CreateIndex(
                name: "IX_Deals_BidId",
                table: "Deals",
                column: "BidId");
        }
    }
}
