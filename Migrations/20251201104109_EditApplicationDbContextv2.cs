using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DeBillPay_Backend.Migrations
{
    /// <inheritdoc />
    public partial class EditApplicationDbContextv2 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_EbillHistories_EbillId",
                table: "EbillHistories",
                column: "EbillId");

            migrationBuilder.AddForeignKey(
                name: "FK_EbillHistories_Ebills_EbillId",
                table: "EbillHistories",
                column: "EbillId",
                principalTable: "Ebills",
                principalColumn: "EbillId",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_EbillHistories_Ebills_EbillId",
                table: "EbillHistories");

            migrationBuilder.DropIndex(
                name: "IX_EbillHistories_EbillId",
                table: "EbillHistories");
        }
    }
}
