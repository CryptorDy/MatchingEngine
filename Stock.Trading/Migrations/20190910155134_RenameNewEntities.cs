using Microsoft.EntityFrameworkCore.Migrations;

namespace Stock.Trading.Migrations
{
    public partial class RenameNewEntities : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_DealsV2_AsksV2_AskId",
                table: "DealsV2");

            migrationBuilder.DropForeignKey(
                name: "FK_DealsV2_BidsV2_BidId",
                table: "DealsV2");

            migrationBuilder.DropPrimaryKey(
                name: "PK_DealsV2",
                table: "DealsV2");

            migrationBuilder.DropPrimaryKey(
                name: "PK_BidsV2",
                table: "BidsV2");

            migrationBuilder.DropPrimaryKey(
                name: "PK_AsksV2",
                table: "AsksV2");

            migrationBuilder.RenameTable(
                name: "DealsV2",
                newName: "Deals");

            migrationBuilder.RenameTable(
                name: "BidsV2",
                newName: "Bids");

            migrationBuilder.RenameTable(
                name: "AsksV2",
                newName: "Asks");

            migrationBuilder.RenameIndex(
                name: "IX_DealsV2_BidId",
                table: "Deals",
                newName: "IX_Deals_BidId");

            migrationBuilder.RenameIndex(
                name: "IX_DealsV2_AskId",
                table: "Deals",
                newName: "IX_Deals_AskId");

            migrationBuilder.AddPrimaryKey(
                name: "PK_Deals",
                table: "Deals",
                column: "DealId");

            migrationBuilder.AddPrimaryKey(
                name: "PK_Bids",
                table: "Bids",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_Asks",
                table: "Asks",
                column: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Deals_Asks_AskId",
                table: "Deals",
                column: "AskId",
                principalTable: "Asks",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Deals_Bids_BidId",
                table: "Deals",
                column: "BidId",
                principalTable: "Bids",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Deals_Asks_AskId",
                table: "Deals");

            migrationBuilder.DropForeignKey(
                name: "FK_Deals_Bids_BidId",
                table: "Deals");

            migrationBuilder.DropPrimaryKey(
                name: "PK_Deals",
                table: "Deals");

            migrationBuilder.DropPrimaryKey(
                name: "PK_Bids",
                table: "Bids");

            migrationBuilder.DropPrimaryKey(
                name: "PK_Asks",
                table: "Asks");

            migrationBuilder.RenameTable(
                name: "Deals",
                newName: "DealsV2");

            migrationBuilder.RenameTable(
                name: "Bids",
                newName: "BidsV2");

            migrationBuilder.RenameTable(
                name: "Asks",
                newName: "AsksV2");

            migrationBuilder.RenameIndex(
                name: "IX_Deals_BidId",
                table: "DealsV2",
                newName: "IX_DealsV2_BidId");

            migrationBuilder.RenameIndex(
                name: "IX_Deals_AskId",
                table: "DealsV2",
                newName: "IX_DealsV2_AskId");

            migrationBuilder.AddPrimaryKey(
                name: "PK_DealsV2",
                table: "DealsV2",
                column: "DealId");

            migrationBuilder.AddPrimaryKey(
                name: "PK_BidsV2",
                table: "BidsV2",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_AsksV2",
                table: "AsksV2",
                column: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_DealsV2_AsksV2_AskId",
                table: "DealsV2",
                column: "AskId",
                principalTable: "AsksV2",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_DealsV2_BidsV2_BidId",
                table: "DealsV2",
                column: "BidId",
                principalTable: "BidsV2",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
