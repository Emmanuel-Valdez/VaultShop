using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VaultShop.DataAccess.Migrations
{
    /// <inheritdoc />
    public partial class AddOrderEmailTrackingColumns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "OrderConfirmationEmailSentUtc",
                table: "OrderHeaders",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "PaymentReceiptEmailSentUtc",
                table: "OrderHeaders",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ShippingConfirmationEmailSentUtc",
                table: "OrderHeaders",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "OrderConfirmationEmailSentUtc",
                table: "OrderHeaders");

            migrationBuilder.DropColumn(
                name: "PaymentReceiptEmailSentUtc",
                table: "OrderHeaders");

            migrationBuilder.DropColumn(
                name: "ShippingConfirmationEmailSentUtc",
                table: "OrderHeaders");
        }
    }
}
