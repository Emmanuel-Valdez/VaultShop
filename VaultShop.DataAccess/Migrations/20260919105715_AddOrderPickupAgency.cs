using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VaultShop.DataAccess.Migrations
{
    /// <inheritdoc />
    public partial class AddOrderPickupAgency : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "DeliveryType",
                table: "OrderHeaders",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PickupAgencyAddress",
                table: "OrderHeaders",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PickupAgencyCode",
                table: "OrderHeaders",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PickupAgencyName",
                table: "OrderHeaders",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DeliveryType",
                table: "OrderHeaders");

            migrationBuilder.DropColumn(
                name: "PickupAgencyAddress",
                table: "OrderHeaders");

            migrationBuilder.DropColumn(
                name: "PickupAgencyCode",
                table: "OrderHeaders");

            migrationBuilder.DropColumn(
                name: "PickupAgencyName",
                table: "OrderHeaders");
        }
    }
}
