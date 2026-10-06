## Context

See `proposal.md` (Why) for motivation. Current state shaping this design:

- Cart identity is `(ApplicationUserId, ProductId)` with single-row reads per product (`Customer/HomeController.Details` POST sums one row; `CartController.Plus` compares one row; `CheckoutService` validates/decrements per product inside one transaction guarded by `CK_Products_StockQuantity_NonNegative`).
- Price is resolved per product at cart/checkout read time (`CartController`, `CheckoutService.GetPrice` → `FinalRetailPrice`/`FinalWholesalePrice`); `ShoppingCart.Price` is `[NotMapped]`.
- The cost-based `PricingCalculatorService` writes prices per product; storefront badges/steppers read `Product.StockQuantity` directly. No product-variant domain model exists (verified by grep: only image-cover "variants" and `ToLowerInvariant` matches).
- Constraints: es-AR default + en-US localization via resx; variants never get URLs (slugs/sitemap/SEO untouched); 2–10 combinations typical, admin UI favors simplicity.

## Goals / Non-Goals

**Goals:**

- Generic variant model with zero hardcoded option types; per-product values; explicit purchasable combinations.
- v1 ships shared stock pool + single price + shared gallery; the schema already carries the future extension point (`ProductVariant` row + `VariantId` plumbing).
- All existing single-row cart/stock reads become per-product sums; no behavior change for variant-less products (nullable `VariantId`, null = legacy row).

**Non-Goals:**

- Per-variant price, stock, or images (model, UI, and pricing-engine integration explicitly excluded; see Decision 6).
- Bulk variant management, cartesian auto-regeneration on value edits, global shared value catalogs.
- Localization of type/value strings beyond plain storage (labels render as stored in v1; proper nouns need none).

## Decisions

### 1. Four new tables + two nullable columns (no join table for product↔type)

- `VariantOptionType(Id, Name unique)` — global reusable names (`Casa`, `Talle`, `Color`).
- `VariantOptionValue(Id, ProductId FK, TypeId FK, Value, SortOrder)` — values are per product (user decision); the set of types a product "uses" is derived as the distinct `TypeId`s among its values. Alternative considered: explicit `ProductVariantOption` join — rejected, one more table for information already implied by the values.
- `ProductVariant(Id, ProductId FK, IsAvailable)` — the purchasable combination; intentionally lean in v1.
- `ProductVariantValue(VariantId FK, ValueId FK)` — composite PK; each combination holds exactly one value per type the product uses (enforced in application logic + a uniqueness approach on `(VariantId, Type value's TypeId)` validated at write time).
- `ShoppingCart.VariantId nullable FK`, `OrderDetail.VariantId nullable FK + VariantLabel string snapshot`. Null = variant-less/legacy row, so existing data and products without variants need no backfill beyond nulls.

### 2. Server resolves and validates VariantId; client only picks values

The detail page renders one selector per type; JS maps the selected value-tuple to a `VariantId` (data attribute per valid combination) and posts it. Server re-validates: variant belongs to the product, its values cover exactly the product's types, and `IsAvailable` is true. Never trust a posted label or value list. Alternative (posting raw value ids and resolving server-side) was considered; posting `VariantId` keeps cart identity a single FK and matches `OrderDetail`'s reference.

### 3. Stock guards sum across sibling variant lines

`HomeController.Details` POST, `CartController.Plus`, and `CheckoutService` validation change from "read the product's single cart row" to "sum `Count` over all cart rows of this product". Decrement stays one subtraction per product of the summed total inside the existing transaction; the `CK_Products_StockQuantity_NonNegative` mapping stays valid. Session cart badge counts lines (unchanged semantics: number of lines, not units).

### 4. Price resolution untouched in v1

`GetPrice` and calculator flows keep reading the product only; variant never influences price. `OrderDetail.Price` snapshot logic unchanged.

### 5. Snapshot format and display

`VariantLabel` built at order creation as `Type: Value, Type: Value` in the product's type order (e.g. `Casa: Gryffindor, Tamaño: 15"`), stored verbatim, rendered as stored in order details/confirmation/summary HTML/PDF/emails. Renames never propagate (by design, per snapshot requirement).

### 6. Explicitly deferred: per-variant price/stock (extension path documented)

When a real product needs it: add nullable `PriceOverrideRetail/Wholesale` and/or `StockQuantity` columns on `ProductVariant`; price resolution becomes "variant override else product"; stock guards switch from per-product sums to per-variant checks with a per-variant non-negative constraint; product-level fields become display-only aggregates for products with variants. No table or identity redesign needed — that is why `ProductVariant` exists as a row today. Rejected for v1 because it forks the pricing engine (calculator is per-product cost-based) and turns every stock badge into selection-dependent UI.

### 7. Admin UX: values first, cartesian generate, prune by hand

Product editor gains a Variants section: (1) add types (pick existing name or create) + values with sort order; (2) "Generate combinations" button producing the cartesian product as explicit rows; (3) per-row available toggle + delete. Value delete blocked while referenced. No auto-regeneration on later value edits (admin adds the new combinations explicitly). Fits the 2–10 combination target; the same button scales if a product ever grows.

## Risks / Trade-offs

- [Stale VariantId posted] → server re-validates ownership, type coverage, and availability on every add; checkout re-resolves lines against current data.
- [Combinatorial growth (types × values)] → explicit rows + prune; no auto-generation beyond the one-shot helper; admin sees the real row count before saving.
- [Missed single-row cart/stock read] → every `ShoppingCart` query keyed by product alone must be audited; new tests assert multi-line sums (see tasks).
- [Label language] → v1 stores admin-entered text verbatim; es-AR/en-US label localization is future work, not a v1 blocker.
- [SEO/slug regressions] → variants have no routes; sitemap/slug code paths untouched; tests assert no new URLs.

## Migration Plan

1. EF Core migration: create 4 tables; add nullable `ShoppingCart.VariantId`, `OrderDetail.VariantId`, `OrderDetail.VariantLabel`. No data backfill needed (null = variant-less).
2. Deploy code + migration together (code reads nulls safely; old rows behave as today).
3. Rollback: if unapplied in production, migration `Down` drops the new tables/columns; no shared-table schema changes exist to unwind.

## Open Questions

- Selector presentation per type (chips vs dropdowns, incl. mobile): deferred to implementation; spec requires one selector per type and mandatory selection, not a specific widget.
