## Why

Deleting a category currently soft-deletes the `Category` row (`IsDeleted=true`) without checking for linked `Product` rows. Products remain `IsDeleted=false` + `IsAvailableInStore=true` and stay purchasable with a deleted category, breaking catalog integrity and pricing that depends on `Category.MaxExpectation`/`AvgShippingCost`. A separate but related gap: `PackagingByCategory` (1:1 with `Category`) has no own soft-delete and is left orphaned. This fixes the data-integrity bug before it creates live orphans (none today, verified via DB probe).

## What Changes

- **Block category deletion when active products exist** — `CategoryController.Delete` checks for `Products.Any(p => CategoryId == id && !IsDeleted)` before soft-deleting. If count > 0, deletion is rejected with a localized error that includes the product count and guidance to reassign first.
- **Hard-delete packaging rows on successful category deletion (option B)** — when deletion is allowed, atomically remove the linked `PackagingByCategory` + its `UnitPackagingByCategory` children for that `CategoryId`. No new column on `PackagingByCategory`; calculator data has no customer-facing audit requirement and units already hard-delete (`PackagingByCategoryController.Delete` is hard-delete).
- **Admin UX feedback** — `category.js` delete flow surfaces the server error via `toastr.error` (no table reload) and preserves the existing success path. Add localized strings for the blocked-deletion message.
- **No sentinel category, no bulk reassign wizard** — reassignment uses existing `Product Upsert` `CategoryList` (only non-deleted categories offered), so admin reassigns product-by-product today. Block just forces that step.

## Capabilities

### New Capabilities
- `catalog`: Category lifecycle and catalog integrity — guards around category deletion, packaging cascade, and admin feedback when referential data blocks deletion.

### Modified Capabilities
- _None_ — no existing spec covers category deletion; this is net-new behavior.

## Impact

- **Code:** `VaultShop.Web/Areas/Admin/Controllers/CategoryController.cs` (delete guard + cascade), `VaultShop.Web/wwwroot/js/category.js` (error handling), `VaultShop.Web/Resources` (i18n), optional follow-up safety filters on storefront `Product` queries (`HomeController`, `SeoController`, `PricingCalculatorService`) deferred as non-blocking.
- **DB:** No schema/migration for B — hard-delete via `IUnitOfWork` within the same `Save()`. Packaging orphans on already-deleted categories (if any) can be cleaned via manual probe; no backfill migration required.
- **APIs/UI:** `POST /{culture}/admin/category/delete/{id}` JSON contract unchanged except `success:false` + localized `message` with count when blocked (already consumed as `data.message` by JS).
- **Ops:** Verification queries for both stores (`vaultshop` + `ukiyostudio`) documented in design.
