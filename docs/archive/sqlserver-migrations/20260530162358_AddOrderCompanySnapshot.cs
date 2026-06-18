using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UkiyoDesigns.DataAccess.Migrations
{
    /// <inheritdoc />
    public partial class AddOrderCompanySnapshot : Migration
    {
        /// <inheritdoc />
		protected override void Up(MigrationBuilder migrationBuilder)
		{
			migrationBuilder.AddColumn<int>(
				name: "CompanyId",
				table: "OrderHeaders",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_OrderHeaders_CompanyId",
                table: "OrderHeaders",
                column: "CompanyId");

            migrationBuilder.AddForeignKey(
                name: "FK_OrderHeaders_Companies_CompanyId",
                table: "OrderHeaders",
                column: "CompanyId",
                principalTable: "Companies",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_OrderHeaders_Companies_CompanyId",
                table: "OrderHeaders");

            migrationBuilder.DropIndex(
                name: "IX_OrderHeaders_CompanyId",
                table: "OrderHeaders");

			migrationBuilder.DropColumn(
				name: "CompanyId",
				table: "OrderHeaders");
		}
    }
}
