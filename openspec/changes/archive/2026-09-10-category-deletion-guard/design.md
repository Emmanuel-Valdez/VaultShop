## Context

See `proposal.md` — the bug is `CategoryController.Delete:88-98` soft-deletes without checking `Products`. Today no orphans exist (verified via DB probe), but `HomeController:41,73,201`, `SeoController:37`, `PricingCalculatorService:90`, and `CheckoutService:102` all filter only `Product.IsDeleted`/`IsAvailableInStore`, never `Category.IsDeleted`. So a deleted category's products would stay purchasable. `PackagingByCategory.cs:12` has no `IsDeleted`; its rows (`UnitsPackagingByCategory` hard-delete already at `PackagingByCategoryController:191`) would be orphaned. No global query filter in `ApplicationDbContext:41` — codebase uses explicit `Where(!IsDeleted)` per call. Multi-store: `vaultshop` + `ukiyostudio` share `vaultshop-platform` `postgres:16-alpine` (see `docker-compose.platform.yml:4`, `postgres-init:22`).

## Goals / Non-Goals

**Goals:**
- Make `POST /{culture}/admin/category/delete/{id}` integrity-safe with a single DB count query and clear localized error containing the active product count.
- On allowed delete, hard-delete the 1:1 `PackagingByCategory` + its `UnitPackagingByCategory` rows atomically (option B — no schema change).
- Fix the admin JS error path so a blocked delete is not silently swallowed.

**Non-Goals:**
- Bulk product reassignment UI — admin uses existing `Product Upsert` `CategoryList` (filtered `!IsDeleted` at `ProductController:48,170`). Reassign flow stays product-by-product.
- Sentinel/default category — YAGNI for a block-only guard.
- Global `HasQueryFilter` for `Category` — explicit per-query guards preferred; safety `&& Category.IsDeleted==false` on storefront reads is a deferred follow-up (see Risks).
- Adding `IsDeleted` to `PackagingByCategory` (option A) — deferred; hard-delete suffices for calculator data.

## Decisions

**D1 — Guard location: inside `CategoryController.Delete` before `IsDeleted=true`.**
- Rationale: smallest diff, follows existing pattern of server-side guards (stock checks in `HomeController:134`, `CartController:354`, `CheckoutService:188`). One `Any`/`Count` query: `_unitOfWork.Product.GetAll(p => p.CategoryId==id && !p.IsDeleted).Count()` or `Any` then `Count` for message. Block → `Json(new { success=false, message=... })` (same shape JS already reads at `category.js:60` `data.message`).
- Alternative: DB FK `ON DELETE RESTRICT` or `HasQueryFilter` — heavier, changes every query semantics; app-layer check matches soft-delete idiom.
- Note: `Update(Category)` path at `CategoryRepository:18` uses `_db.Categories.Update` — no category rename path touches this guard.

**D2 — Packaging cascade: hard-delete, not soft-delete.**
- Rationale: `PackagingByCategory.cs` lacks `IsDeleted`; calculator rows have no audit retention need; child units already hard-delete. Cascade inside same `Delete` transaction before `Save()`: fetch `PackagingByCategory` where `CategoryId==id` (include `UnitPackagingByCategoryList`), call `_unitOfWork.UnitPackagingByCategory.RemoveRange(children)` then `_unitOfWork.PackagingByCategory.Remove(parent)`. No migration.
- Alternative A (soft-delete): add `IsDeleted` + migration + filter `GetPackagingTotalsByCategory:229` and `PackagingByCategoryController:152`. More ceremony for a 1:1 that is recreated on demand.
- Alternative C (block on packaging): over-strict — two-step delete.

**D3 — JS handling: branch on `data.success` instead of unconditional `toastr.success` + `reload`.**
- Current `category.js:58-63` always `success → reload + toastr.success`. Change to `if (data.success) { reload; toastr.success } else { toastr.error(data.message) }` and add `error:` handler for network/500. Keeps existing Swal confirm flow.
- Also fix `CategoryController.Delete` to return same `Json(success, message)` shape on blocked path (aligns with `PackagingByCategoryController:189` error shape).

**D4 — i18n: paramized message key with count.**
- Add resource key like `DeleteBlockedHasProducts` → `"Cannot delete: category has {0} active product(s). Reassign them to another category first."` (es-AR counterpart). `IStringLocalizer<CategoryController>` formats with `count`. Keeps `DeleteSuccesfully` + `ErrorWhileDeleting` for success/failure paths.

**D5 — Transaction and ordering.**
- One `Save()` after both `Category.IsDeleted=true` and packaging removes — EF `ApplicationDbContext` batches in one transaction per `Save()`. Use `IUnitOfWork.Save()` once, after all mutations, mirroring `ProductController.Delete:259-261` (single Save).
- No `ExecuteInTransaction` needed — single Save is enough; checkout's explicit transaction is for multi-read consistency, not needed here.

## Risks / Trade-offs

- **Orphans already exist?** → probe returns 0 today, but if hidden via direct SQL, guard won't fix retro. Mitigation: document VPS verification queries for both stores (design appendix) and optional manual cleanup: `DELETE FROM "UnitsPackagingByCategory" WHERE "CategoryId" IN (SELECT "Id" FROM "Categories" WHERE "IsDeleted"=true)` then same for `PackagingsByCategory`. Guard prevents new orphans.
- **Storefront safety net missing** → even with guard, if someone manually flips `Category.IsDeleted` via SQL bypassing the controller, products still show. Mitigation: deferred follow-up adds `&& p.Category.IsDeleted==false` to `HomeController.Index:41`, `Details:73`, `Search:201`, `SeoController:37`, `PricingCalculatorService:90`. Not in this change — keep diff minimal.
- **Race: product reassigned concurrently while delete runs** → `Count` then `Save` is not serializable; low risk (admin-only, single-actor). Mitigation: acceptable; if needed later, re-check `Any` inside a transaction or add row version — not now.
- **Packaging cascade orphan Units** → if `UnitPackagingByCategory` rows exist without parent (shouldn't with FK, but verify), hard-delete parent alone could leave children? Mitigation: delete children first or rely on DB cascade; explicit `RemoveRange` covers both.
- **JS silent failure** → existing `category.js` lacks `error:` callback, so 500/network stalls. Mitigation: add `error: function(xhr){ toastr.error(...) }` so blocked-delete regressions are visible.

## Migration Plan

- No EF migration for B.
- Deploy: code-only, both stores share same image. No feature flag.
- Rollback: revert controller + JS; already-deleted categories stay deleted, packaging rows already removed are gone (hard-delete is intentional — rollback cannot restore them without DB restore).
- Verification: `dotnet build`, `dotnet test` (new controller tests), manual admin delete blocked vs allowed, plus VPS probes below.

### VPS verification (both stores, avoids `$POSTGRES_USER` host-shell trap)

```bash
# run from /opt/vaultshop — vars live in .platform.env, not host shell
docker compose --env-file .platform.env -f docker-compose.platform.yml exec postgres sh -c 'psql -U "$POSTGRES_USER" -d "$POSTGRES_DB" -c '\''SELECT count(*) AS orphan_products FROM "Products" p JOIN "Categories" c ON p."CategoryId"=c."Id" WHERE p."IsDeleted"=false AND c."IsDeleted"=true;'\'''
docker compose --env-file .platform.env -f docker-compose.platform.yml exec postgres sh -c 'psql -U "$UKIYO_POSTGRES_USER" -d "$UKIYO_POSTGRES_DATABASE" -c '\''SELECT count(*) AS orphan_products FROM "Products" p JOIN "Categories" c ON p."CategoryId"=c."Id" WHERE p."IsDeleted"=false AND c."IsDeleted"=true;'\'''

# packaging orphans
docker compose --env-file .platform.env -f docker-compose.platform.yml exec postgres sh -c 'psql -U "$POSTGRES_USER" -d "$POSTGRES_DB" -c '\''SELECT count(*) AS orphan_packagings FROM "PackagingsByCategory" pbc JOIN "Categories" c ON pbc."CategoryId"=c."Id" WHERE c."IsDeleted"=true;'\'''
docker compose --env-file .platform.env -f docker-compose.platform.yml exec postgres sh -c 'psql -U "$UKIYO_POSTGRES_USER" -d "$UKIYO_POSTGRES_DATABASE" -c '\''SELECT count(*) AS orphan_packagings FROM "PackagingsByCategory" pbc JOIN "Categories" c ON pbc."CategoryId"=c."Id" WHERE c."IsDeleted"=true;'\'''

# fallback (no compose env): superuser can query any DB
docker exec vaultshop-platform-postgres-1 psql -U vaultshop_app -d vaultshop -c 'SELECT count(*) FROM "Products" p JOIN "Categories" c ON p."CategoryId"=c."Id" WHERE p."IsDeleted"=false AND c."IsDeleted"=true;'
docker exec vaultshop-platform-postgres-1 psql -U vaultshop_app -d ukiyostudio -c 'SELECT count(*) FROM "Products" p JOIN "Categories" c ON p."CategoryId"=c."Id" WHERE p."IsDeleted"=false AND c."IsDeleted"=true;'
```

## Open Questions

- None blocking. Follow-up storefront `Category.IsDeleted` filters are a separate change if manual SQL bypass is a concern — this guard closes the UI path.
