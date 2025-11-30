using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DeBillPay_Backend.Migrations
{
    /// <inheritdoc />
    public partial class AddNullValueInColumnEbillId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Notifications_Ebills_EbillId",
                table: "Notifications");

            migrationBuilder.AlterColumn<int>(
                name: "EbillId",
                table: "Notifications",
                type: "integer",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AddForeignKey(
                name: "FK_Notifications_Ebills_EbillId",
                table: "Notifications",
                column: "EbillId",
                principalTable: "Ebills",
                principalColumn: "EbillId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Notifications_Ebills_EbillId",
                table: "Notifications");

            migrationBuilder.AlterColumn<int>(
                name: "EbillId",
                table: "Notifications",
                type: "integer",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Notifications_Ebills_EbillId",
                table: "Notifications",
                column: "EbillId",
                principalTable: "Ebills",
                principalColumn: "EbillId",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
