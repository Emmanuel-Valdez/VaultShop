using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VaultShop.DataAccess.Migrations
{
    /// <inheritdoc />
    public partial class AddProductAndCategorySlugs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Slug",
                table: "Products",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Slug",
                table: "Categories",
                type: "text",
                nullable: true);

            BackfillSlugs(migrationBuilder, "Products", "producto");
            BackfillSlugs(migrationBuilder, "Categories", "categoria");

            migrationBuilder.CreateIndex(
                name: "IX_Products_Slug",
                table: "Products",
                column: "Slug",
                unique: true,
                filter: "\"IsDeleted\" = false");

            migrationBuilder.CreateIndex(
                name: "IX_Categories_Slug",
                table: "Categories",
                column: "Slug",
                unique: true,
                filter: "\"IsDeleted\" = false");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Products_Slug",
                table: "Products");

            migrationBuilder.DropIndex(
                name: "IX_Categories_Slug",
                table: "Categories");

            migrationBuilder.DropColumn(
                name: "Slug",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "Slug",
                table: "Categories");
        }

        /// <summary>
        /// Generates slugs for existing non-deleted rows, mirroring <c>SlugHelper.Slugify</c> in SQL
        /// (NFKD fold to printable ASCII, non-alphanumeric runs to a single hyphen, trim hyphens).
        /// Duplicate names get -2, -3, ... by Id order; rows that fold to nothing (CJK-only names)
        /// fall back to {prefix}-{id}. Already-set slugs are left untouched.
        /// </summary>
        private static void BackfillSlugs(MigrationBuilder migrationBuilder, string table, string prefix)
        {
            migrationBuilder.Sql($@"
WITH folded AS (
    SELECT ""Id"",
           NULLIF(trim(both '-' FROM regexp_replace(
               lower(regexp_replace(normalize(""Name"", NFKD), '[^ -~]', '', 'g')),
               '[^a-z0-9]+', '-', 'g')), '') AS ""Base""
    FROM ""{table}""
    WHERE ""IsDeleted"" = false AND ""Slug"" IS NULL
),
numbered AS (
    SELECT ""Id"", ""Base"", row_number() OVER (PARTITION BY ""Base"" ORDER BY ""Id"") AS ""N""
    FROM folded
)
UPDATE ""{table}"" AS t
SET ""Slug"" = CASE
        WHEN numbered.""Base"" IS NULL THEN '{prefix}-' || numbered.""Id""
        WHEN numbered.""N"" = 1 THEN numbered.""Base""
        ELSE numbered.""Base"" || '-' || numbered.""N""
    END
FROM numbered
WHERE t.""Id"" = numbered.""Id"";");
        }
    }
}
