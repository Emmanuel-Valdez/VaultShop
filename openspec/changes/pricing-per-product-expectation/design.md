## Context

See `proposal.md` — Why. Fixed-cost share is computed in `PricingCalculatorService.GetCostByProducts()` as `fixedCost / product.Category.MaxExpectation`, and the same figure feeds `CostByProductView.MaxExpectationMonthly` and the final-price suggestions. `MaxExpectation` currently lives only on `Category` (validated 1–10000, app-level only, no DB constraint) and is collected only in the category upsert form. Fabric/garment-hardware costs are already per-product; this moves the last category-derived cost input to the product.

## Goals / Non-Goals

**Goals:**
- Products own their monthly expectation; fixed-cost share computed per product.
- Migration preserves today's numbers exactly (per category → per product backfill), so the only behavioral shift for existing products is the ability to differentiate expectations per product.
- Zero new dependencies, zero new localization keys, reuses existing validation pattern.

**Non-Goals:**
- Changing packaging cost (stays per-category) or `AvgShippingCost` (stays per-category).
- Automatic re-publish of suggested prices — the existing "Publish suggested prices" action is the correction path, ran once after deployment.
- Any storefront-visible change — expectation is a costing heuristic only.

## Decisions

**D1: Full move (option A) — `Category.MaxExpectation` is removed, not kept as a fallback.**
Rationale: per-product *is* per-product; a category fallback creates two sources of truth where a later category edit silently shifts untouched products. The migration backfill gives the same result for free (frozen copies). Alternative considered (B, nullable fallback `product.MaxExpectation ?? category.MaxExpectation`) was rejected — it keeps the category field alive and complicates the service with a fallback chain for a behavior nobody asked for.

**D2: Single EF migration with raw-SQL backfill.**
```
Up:
  AddColumn(MaxExpectation, int, non-nullable, defaultValue: 0) on Products
  Sql UPDATE "Products" p SET "MaxExpectation" = c."MaxExpectation"
      FROM "Categories" c WHERE p."CategoryId" = c."Id"
  DropColumn(MaxExpectation) on Categories
Down:
  AddColumn(MaxExpectation, int, non-nullable, defaultValue: 1) on Categories
  DropColumn(MaxExpectation) on Products
```
The `defaultValue: 0` only guarantees the `ADD COLUMN NOT NULL` succeeds; the backfill then overwrites every row, and the model's `LocalizedRange(1,1000)` guards all future writes. `dotnet ef migrations add` generates the schema changes; the `UPDATE` (and the Down `AddColumn`) are hand-added to the generated migration. Snapshot regenerated automatically.

**D3: Validation mirrors the old category field exactly** — `[LocalizedRequired]` + `[LocalizedRange(1, 10000)]` on `Product.MaxExpectation`, app-level only (no DB check constraint; consistent with how `Category` was). No divide-by-zero risk: range starts at 1, and migration source values were validated on entry.

**D4: Default 30 is a form-time prefill, not a DB default.** The `ProductController.Upsert` create branch (`ProductVM.Product` init at `ProductController.cs:53`) sets `MaxExpectation = 30`. Schema keeps no default so an explicit value is always persisted.

**D5: Keep the view/JSON field name.** `MaxExpectationMonthly` (`CostByProductView`, `costByProduct.js` column) is populated with `product.MaxExpectation`; the JS needs no change. `CostByProductView` moves from Class Library — it carries the value as a property; only the assignment source changes.

**D6: Prune category-side references.** Remove the two validation attributes from `Category.cs`, the input block from `Category/Upsert.cshtml`, and the `obj.Name == obj.MaxExpectation.ToString()` arm of the name-collision guard at `CategoryController.cs:49`.

**D7: Wholesale-restriction → non-blocking SweetAlert.** Remove the server-side `ModelState.AddModelError` block at `ProductController.Upsert` (`ProductController.cs:82-85`). Add a client-side SweetAlert2 confirm in `Product/Upsert.cshtml` that fires on submit when `FinalRetailPrice < FinalWholesalePrice`, using the existing `MinorLowerMajor` localized value. SweetAlert2 is already CDN-loaded in `_Layout.cshtml:383` so no new dependency is needed. The save proceeds regardless.

**D8: es-AR calculated-price label.** Change `Product/Upsert.es.resx` `ListPrice` from "Precio Calculado" to "Precio Calculado Sugerido"; the `.en.resx` value stays unchanged. The `GetTranslations` endpoint is unaffected — the label is server-rendered by Razor only.

**D9: Help-docs update.** Update `_HelpProducts.cshtml`, `_HelpCosts.cshtml`, and `_HelpFinalPrices.cshtml` to reflect: (1) monthly expectation is per product (prefilled 30 for new products), (2) the retail-below-wholesale non-blocking warning exists. Help is Spanish-only hardcoded text; no localization concerns.

## Risks / Trade-offs

- **Every existing product becomes "outdated" after upgrade** → prices shift once expectations are per-product only if the admin changes them; since backfill copies category values, prices stay identical until the admin edits an expectation. Only edited products drift, and the existing "Publish suggested prices" flow re-baselines them.
- **Backfilled values are frozen** — raising a category's expectation no longer cascades to its products. This is the intended semantics (per-product planning numbers), documented so a future admin doesn't expect cascade behavior.
- **EF `AddColumn` default lingers in schema** → harmless (all writes flow through the validated model); not worth an extra `DropColumn`-of-default step in the same migration.
- **Down migration cannot reconstruct category values** → a true downgrade loses per-product data; rollback strategy is restore-from-backup, not `migrate down` (consistent with this project's forward-only DB deploy).
- **Test seeds referencing `Category.MaxExpectation` break at compile** → mechanical: move the value into `Product.MaxExpectation` in `PricingCalculatorServiceTests`, `CheckoutServiceIntegrationTests`, `CartCheckoutHttpTests`, `SearchHttpTests`, `HomeControllerAddToCartStockTests`; drop it from `Category` initializers.

## Migration Plan

1. Deploy code + migration via the normal `docker-compose` build (migrations run on startup/apply as today).
2. Post-deploy, admin opens Product Price (`/admin/ProductPrice`) once → any drifted products flagged → "Publish suggested prices" re-baselines. With pure backfill no drift is expected; differentiation appears only after expectations are edited.
3. Rollback: restore DB from backup (forward-only schema change).

## Open Questions

None — the design falls out of the chosen option A (full move to per-product expectation, range 1–10000, 30 prefill), the non-blocking wholesale warning (D7), the es-AR label change (D8), and the help-docs update (D9). All four are direct extensions of the base move and introduce no further decisions.