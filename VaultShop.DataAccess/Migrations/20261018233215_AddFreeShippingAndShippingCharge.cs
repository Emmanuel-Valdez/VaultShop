using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VaultShop.DataAccess.Migrations
{
    /// <inheritdoc />
    public partial class AddFreeShippingAndShippingCharge : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "FreeShipping",
                table: "Products",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<decimal>(
                name: "ShippingCharge",
                table: "OrderHeaders",
                type: "numeric(18,2)",
                nullable: false,
                defaultValue: 0m);

            // Backfill: products that existed before this migration already bake the category
            // average shipping cost into their retail price, so they are free shipping. Rows
            // inserted afterwards take the model default (false).
            migrationBuilder.Sql("UPDATE \"Products\" SET \"FreeShipping\" = true;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "FreeShipping",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "ShippingCharge",
                table: "OrderHeaders");
        }
    }
}
