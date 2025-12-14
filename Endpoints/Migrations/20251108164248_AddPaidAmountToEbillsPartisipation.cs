using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable


namespace DeBillPay_Backend.Migrations
{
    /// <inheritdoc />
    public partial class AddPaidAmountToEbillsPartisipation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "PaidAmount",
                table: "EbillParticipants",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PaidAmount",
                table: "EbillParticipants");
        }
    }
}
