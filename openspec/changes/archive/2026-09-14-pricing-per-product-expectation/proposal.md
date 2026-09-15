## Why

Fixed-cost allocation uses a single monthly expectation per `Category` (`fixedCosts / category.MaxExpectation`), so every product in a category absorbs the same fixed-cost share even when their actual sales expectations differ wildly. Products need their own expectation so price suggestions reflect each product's expected volume. The expectation is the only category-derived cost input left in pricing (fabric, garment hardware are already per-product), so this closes the granularity gap.

## What Changes

- Add `MaxExpectation` (monthly production expectation) to `Product` as a required field, validated 1–10000, matching the existing `Category.MaxExpectation` validation pattern.
- **BREAKING** (model/DB): compute fixed-cost allocation from `product.MaxExpectation` instead of `product.Category.MaxExpectation`. All previously published prices shift and are marked *outdated* until re-published through the existing "Publish suggested prices" flow.
- **BREAKING** (model/DB): remove `Category.MaxExpectation` from the `Category` model and database after migrating existing values to products.
- Migration backfills each product with its category's current expectation, preserving existing values (correction path: re-publish suggested prices once after migration).
- New-product upsert defaults `Product.MaxExpectation` to `30` as a suggested starting amount.
- Admin product upsert gains the expectation input; admin category upsert loses it.
- Cost-by-product view reports the per-product expectation.
- **BREAKING** (UX): the admin product upsert no longer blocks saving when `FinalRetailPrice` is below `FinalWholesalePrice`; the save proceeds and a non-blocking SweetAlert2 warning informs the admin.
- The calculated-price field on the product form is labeled "Precio Calculado Sugerido" in Spanish (es-AR only; English label unchanged).
- Help section (product creation, costs, final prices) updated to match the new per-product expectation and the new warning.

## Capabilities

### New Capabilities
- `pricing`: per-product monthly expectation as the basis for fixed-cost allocation in price suggestions; category no longer carries the expectation.

### Modified Capabilities
<!-- none: the existing `catalog` spec only covers category-deletion guards and is unaffected. -->

## Impact

- **Models**: `VaultShop.Models/Product.cs` (add `MaxExpectation` + validation), `VaultShop.Models/Category.cs` (remove `MaxExpectation` + validation).
- **DB**: one EF migration — add column to `Products`, backfill from `Categories`, drop column from `Categories`. Snapshot (`ApplicationDbContextModelSnapshot.cs`) regenerated.
- **Pricing service**: `VaultShop.Web/Services/Pricing/PricingCalculatorService.cs` (`GetCostByProducts` — read expectation from product; `CostByProductView.MaxExpectationMonthly` carries `product.MaxExpectation`).
- **Admin controllers**: `ProductController.Upsert` (default 30 on create; drop the blocking `MinorLowerMajor` guard), `CategoryController.Upsert` (drop the name-collision check line that references `MaxExpectation`).
- **Admin views**: `Product/Upsert.cshtml` (new input + non-blocking SweetAlert2 warning on submit), `Category/Upsert.cshtml` (remove input), `wwwroot/js/costByProduct.js` (column now renders product value — same field name, no JS change expected).
- **Help docs**: `_HelpProducts.cshtml`, `_HelpCosts.cshtml`, `_HelpFinalPrices.cshtml` updated (per-product expectation, default 30, below-wholesale warning).
- **Tests**: expectation moves from Category to Product seed data in `PricingCalculatorServiceTests`, `CheckoutServiceIntegrationTests`, `CartCheckoutHttpTests`, `SearchHttpTests`, `HomeControllerAddToCartStockTests`.
- **Localization**: adds `MaxExpMonthly` to `Product/Upsert` es+en resx (reusing the category form's value); changes `Product/Upsert.es.resx` `ListPrice` value to "Precio Calculado Sugerido" (es-AR only, `.en` untouched). `GetTranslations` unchanged — the new label is server-rendered only.