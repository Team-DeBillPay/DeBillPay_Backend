using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DeBillPay_Backend.Migrations
{
    /// <inheritdoc />
    public partial class UptateTableNotification : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "GroupId",
                table: "Notifications",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Notifications_GroupId",
                table: "Notifications",
                column: "GroupId");

            migrationBuilder.AddForeignKey(
                name: "FK_Notifications_Groups_GroupId",
                table: "Notifications",
                column: "GroupId",
                principalTable: "Groups",
                principalColumn: "GroupId");
        }


        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Notifications_Groups_GroupId",
                table: "Notifications");

            migrationBuilder.DropIndex(
                name: "IX_Notifications_GroupId",
                table: "Notifications");

            migrationBuilder.DropColumn(
                name: "GroupId",
                table: "Notifications");
        }
    }
}
