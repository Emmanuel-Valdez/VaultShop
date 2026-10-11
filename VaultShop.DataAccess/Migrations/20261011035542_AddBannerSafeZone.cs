using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VaultShop.DataAccess.Migrations
{
    /// <inheritdoc />
    public partial class AddBannerSafeZone : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "SafeZoneHeight",
                table: "Banners",
                type: "numeric(4,3)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "SafeZoneLeft",
                table: "Banners",
                type: "numeric(4,3)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "SafeZoneTop",
                table: "Banners",
                type: "numeric(4,3)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "SafeZoneWidth",
                table: "Banners",
                type: "numeric(4,3)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SafeZoneHeight",
                table: "Banners");

            migrationBuilder.DropColumn(
                name: "SafeZoneLeft",
                table: "Banners");

            migrationBuilder.DropColumn(
                name: "SafeZoneTop",
                table: "Banners");

            migrationBuilder.DropColumn(
                name: "SafeZoneWidth",
                table: "Banners");
        }
    }
}
