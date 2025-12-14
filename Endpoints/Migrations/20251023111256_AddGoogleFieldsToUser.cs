using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable


namespace DeBillPay_Backend.Migrations
{
    /// <inheritdoc />
    public partial class AddGoogleFieldsToUser : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "GoogleId",
                table: "Users",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "GoogleId",
                table: "Users");
        }
    }
}
