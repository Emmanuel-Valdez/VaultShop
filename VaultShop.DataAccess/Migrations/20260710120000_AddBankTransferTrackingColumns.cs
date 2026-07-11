using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VaultShop.DataAccess.Migrations
{
    /// <inheritdoc />
    public partial class AddBankTransferTrackingColumns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "AdminBankTransferAlertEmailSentUtc",
                table: "OrderHeaders",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "TransferConfirmedByCustomerAt",
                table: "OrderHeaders",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AdminBankTransferAlertEmailSentUtc",
                table: "OrderHeaders");

            migrationBuilder.DropColumn(
                name: "TransferConfirmedByCustomerAt",
                table: "OrderHeaders");
        }
    }
}
