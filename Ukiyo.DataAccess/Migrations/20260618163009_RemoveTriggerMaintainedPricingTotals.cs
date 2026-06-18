using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UkiyoDesigns.DataAccess.Migrations
{
    /// <inheritdoc />
    public partial class RemoveTriggerMaintainedPricingTotals : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "UnitTotal",
                table: "UnitsPackagingByCategory");

            migrationBuilder.DropColumn(
                name: "UnitTotal",
                table: "UnitsGarmentHardwareByProduct");

            migrationBuilder.DropColumn(
                name: "UnitTotal",
                table: "UnitsFabricByProduct");

            migrationBuilder.DropColumn(
                name: "TotalPackagingByCategory",
                table: "PackagingsByCategory");

            migrationBuilder.DropColumn(
                name: "TotalGarmentHardwareByProduct",
                table: "GarmentHardwaresByProduct");

            migrationBuilder.DropColumn(
                name: "TotalFabricByProduct",
                table: "FabricsByProduct");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "UnitTotal",
                table: "UnitsPackagingByCategory",
                type: "numeric(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "UnitTotal",
                table: "UnitsGarmentHardwareByProduct",
                type: "numeric(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "UnitTotal",
                table: "UnitsFabricByProduct",
                type: "numeric(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "TotalPackagingByCategory",
                table: "PackagingsByCategory",
                type: "numeric(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "TotalGarmentHardwareByProduct",
                table: "GarmentHardwaresByProduct",
                type: "numeric(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "TotalFabricByProduct",
                table: "FabricsByProduct",
                type: "numeric(18,2)",
                nullable: false,
                defaultValue: 0m);
        }
    }
}
