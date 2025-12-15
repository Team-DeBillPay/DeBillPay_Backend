using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable


namespace DeBillPay_Backend.Migrations
{
    /// <inheritdoc />
    public partial class MakeEbillIdNullable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Invitations_Ebills_EbillId",
                table: "Invitations");

            migrationBuilder.AlterColumn<int>(
                name: "EbillId",
                table: "Invitations",
                type: "integer",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AddForeignKey(
                name: "FK_Invitations_Ebills_EbillId",
                table: "Invitations",
                column: "EbillId",
                principalTable: "Ebills",
                principalColumn: "EbillId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Invitations_Ebills_EbillId",
                table: "Invitations");

            migrationBuilder.AlterColumn<int>(
                name: "EbillId",
                table: "Invitations",
                type: "integer",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Invitations_Ebills_EbillId",
                table: "Invitations",
                column: "EbillId",
                principalTable: "Ebills",
                principalColumn: "EbillId",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
