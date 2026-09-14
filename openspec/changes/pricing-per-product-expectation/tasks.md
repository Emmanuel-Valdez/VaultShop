## 1. Model & Migration

- [x] 1.1 Add `MaxExpectation` to `VaultShop.Models/Product.cs` with `[LocalizedRequired]` and `[LocalizedRange(1, 10000, …)]` mirroring the old category attributes and verify the project builds
- [x] 1.2 Remove `MaxExpectation` + its two validation attributes from `VaultShop.Models/Category.cs` and verify the project builds
- [x] 1.3 Generate the EF migration (e.g. `MoveMaxExpectationToProducts`) and hand-add the backfill `UPDATE "Products" SET "MaxExpectation" = c."MaxExpectation" FROM "Categories" c`, plus the matching Down steps per design D2; verify `dotnet ef migrations add` output contains the new Products column, the category drop, the UPDATE, and a `dotnet build VaultShop.sln` succeeds

## 2. Pricing Service

- [x] 2.1 Update `PricingCalculatorService.GetCostByProducts()` so the fixed-cost share is `fixedCost / product.MaxExpectation` and `CostByProductView.MaxExpectationMonthly` receives `product.MaxExpectation`; verify the existing pricing calculator tests compile and pass
- [x] 2.2 Add a calculator test where two products in one category have different expectations and assert different fixed-cost shares (e.g. 10 vs 20 expectations over 1000 fixed → 100 vs 50, per spec Scenario) and verify `dotnet test` runs it green

## 3. Admin UI

- [x] 3.1 In `ProductController.Upsert` set the new-product default `MaxExpectation = 30` (create branch) and verify the create view renders 30 pre-filled
- [x] 3.2 Add the monthly expectation input (`Product.MaxExpectation`) to `Product/Upsert.cshtml` with the existing localized label and validation span; verify create AND edit forms show the field and reject 0 / >10000 / empty with localized errors
- [x] 3.3 Remove the expectation input from `Category/Upsert.cshtml` and prune the `obj.Name == obj.MaxExpectation.ToString()` arm in `CategoryController.Upsert`; verify category create/edit still passes the remaining guards and saves
- [x] 3.4 Verify the cost-by-product report (`/admin/ProductPrice/CostByProduct`, `costByProduct.js`) renders each row with the product's own expectation and the matching fixed-cost share (browser check; JS column unchanged)

## 4. Tests & Data

- [x] 4.1 Move the expectation seed from `Category` to `Product` in `PricingCalculatorServiceTests`, `CheckoutServiceIntegrationTests`, `CartCheckoutHttpTests`, `SearchHttpTests`, and `HomeControllerAddToCartStockTests`; verify `dotnet test VaultShop.sln` is fully green
- [x] 4.2 Add a validation check (extend an existing upsert/model test) asserting product expectations of 0 and 10001 are invalid and 1/10000 valid; verify it passes
- [x] 4.3 After implementation, confirm the data-correction path: run the calculator in-app, confirm a changed expectation marks products outdated, and "Publish suggested prices" re-baselines without error

## 5. Product Creation UX (warning, label, help)

- [x] 5.1 Remove the blocking `MinorLowerMajor` guard in `ProductController.Upsert` (`ProductController.cs:82-85`) and add a non-blocking SweetAlert2 warning in `Product/Upsert.cshtml` that fires on submit when `FinalRetailPrice < FinalWholesalePrice`; verify the product still saves with the warning shown and saves without it otherwise
- [x] 5.2 Change `Product/Upsert.es.resx` `ListPrice` value to "Precio Calculado Sugerido" (es-AR only) and verify `Upsert.en.resx` is unchanged
- [x] 5.3 Update the help partials `_HelpProducts.cshtml`, `_HelpCosts.cshtml`, `_HelpFinalPrices.cshtml` for the per-product expectation (default 30) and the below-wholesale warning; verify the Help page renders and reads correctly