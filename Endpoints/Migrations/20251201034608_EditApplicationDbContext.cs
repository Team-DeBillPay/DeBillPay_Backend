using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable


namespace DeBillPay_Backend.Migrations
{
    /// <inheritdoc />
    public partial class EditApplicationDbContext : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Notifications_Ebills_EbillId",
                table: "Notifications");

            migrationBuilder.AddForeignKey(
                name: "FK_Notifications_Ebills_EbillId",
                table: "Notifications",
                column: "EbillId",
                principalTable: "Ebills",
                principalColumn: "EbillId",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Notifications_Ebills_EbillId",
                table: "Notifications");

            migrationBuilder.AddForeignKey(
                name: "FK_Notifications_Ebills_EbillId",
                table: "Notifications",
                column: "EbillId",
                principalTable: "Ebills",
                principalColumn: "EbillId");
        }
    }
}
