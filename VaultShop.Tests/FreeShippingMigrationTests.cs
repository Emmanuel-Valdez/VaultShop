using System.Reflection;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using VaultShop.DataAccess.Migrations;

namespace VaultShop.Web.Tests
{
    // envio-gratis 1.1 — guards the backfill: re-generating the migration must not silently drop
    // the UPDATE that flags pre-existing products free, nor start touching stored prices.
    public class FreeShippingMigrationTests
    {
        private static List<MigrationOperation> UpOperations()
        {
            var migration = new AddFreeShippingAndShippingCharge();
            var builder = new MigrationBuilder("Npgsql.EntityFrameworkCore.PostgreSQL");
            var up = typeof(AddFreeShippingAndShippingCharge)
                .GetMethod("Up", BindingFlags.Instance | BindingFlags.NonPublic)!;
            up.Invoke(migration, new object[] { builder });
            return builder.Operations.ToList();
        }

        [Fact]
        public void AddsFreeShippingColumn_DefaultingOff()
        {
            var op = Assert.Single(UpOperations().OfType<AddColumnOperation>(),
                o => o.Table == "Products" && o.Name == "FreeShipping");

            Assert.Equal("boolean", op.ColumnType);
            Assert.Equal(false, op.DefaultValue);
        }

        [Fact]
        public void AddsShippingChargeColumn_DefaultingZero()
        {
            var op = Assert.Single(UpOperations().OfType<AddColumnOperation>(),
                o => o.Table == "OrderHeaders" && o.Name == "ShippingCharge");

            Assert.Equal("numeric(18,2)", op.ColumnType);
            Assert.Equal(0m, op.DefaultValue);
        }

        [Fact]
        public void BackfillsPreExistingProductsToFree()
        {
            var sql = Assert.Single(UpOperations().OfType<SqlOperation>());

            Assert.Contains("\"FreeShipping\" = true", sql.Sql);
            Assert.DoesNotContain("Price", sql.Sql);
        }
    }
}
