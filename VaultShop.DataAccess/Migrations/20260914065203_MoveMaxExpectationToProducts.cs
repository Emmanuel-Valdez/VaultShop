using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VaultShop.DataAccess.Migrations
{
    /// <inheritdoc />
    public partial class MoveMaxExpectationToProducts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "MaxExpectation",
                table: "Products",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.Sql(@"UPDATE ""Products"" p SET ""MaxExpectation"" = c.""MaxExpectation"" FROM ""Categories"" c WHERE p.""CategoryId"" = c.""Id""");

            migrationBuilder.DropColumn(
                name: "MaxExpectation",
                table: "Categories");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "MaxExpectation",
                table: "Products");

            migrationBuilder.AddColumn<int>(
                name: "MaxExpectation",
                table: "Categories",
                type: "integer",
                nullable: false,
                defaultValue: 1);
        }
    }
}
