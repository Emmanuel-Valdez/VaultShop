using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VaultShop.DataAccess.Migrations
{
    /// <inheritdoc />
    public partial class AddBranchHoursAndCascadeIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Hours",
                table: "PostalAgencies",
                type: "text",
                nullable: false,
                defaultValue: "no informa");

            migrationBuilder.AddColumn<string>(
                name: "PickupAgencyHours",
                table: "OrderHeaders",
                type: "text",
                nullable: false,
                defaultValue: "no informa");

            migrationBuilder.CreateIndex(
                name: "IX_PostalAgencies_ProvinceCode",
                table: "PostalAgencies",
                column: "ProvinceCode");

            migrationBuilder.CreateIndex(
                name: "IX_PostalAgencies_ProvinceCode_Locality",
                table: "PostalAgencies",
                columns: new[] { "ProvinceCode", "Locality" })
                .Annotation("Npgsql:IndexInclude", new[] { "Code", "Name" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_PostalAgencies_ProvinceCode",
                table: "PostalAgencies");

            migrationBuilder.DropIndex(
                name: "IX_PostalAgencies_ProvinceCode_Locality",
                table: "PostalAgencies");

            migrationBuilder.DropColumn(
                name: "Hours",
                table: "PostalAgencies");

            migrationBuilder.DropColumn(
                name: "PickupAgencyHours",
                table: "OrderHeaders");
        }
    }
}
