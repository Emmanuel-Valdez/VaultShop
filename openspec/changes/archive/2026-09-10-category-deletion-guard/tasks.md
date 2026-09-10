## 1. Guard category deletion when active products exist

- [x] 1.1 Add active-product check to `CategoryController.Delete` (count `Products` where `CategoryId==id && !IsDeleted`) and return `Json { success:false, message=localized count + reassignment guidance }` when count>0; verify by invoking delete on a category with 2 active + 1 soft-deleted product and observing `success:false` with count=2 and `IsDeleted` stays false
- [x] 1.2 Allow delete when count==0 — soft-delete `Category.IsDeleted=true` continues; verify by deleting an empty category and confirming `IsDeleted=true` and `GetAll` no longer returns it
- [x] 1.3 Add localized resource string `DeleteBlockedHasProducts` (es-AR + en-US) paramized with `{0}` count and reassignment guidance wired via `IStringLocalizer<CategoryController>`; verify both cultures render correctly in the JSON message

## 2. Hard-delete packaging cascade on successful delete (option B)

- [x] 2.1 In `CategoryController.Delete` after the guard passes, fetch `PackagingByCategory` for the `CategoryId` (include `UnitPackagingByCategoryList`), hard-delete children then parent before the single `Save()`; verify by deleting a category that has 1 packaging row + 2 unit rows and asserting zero rows for that `CategoryId` afterward
- [x] 2.2 Handle no-packaging case — no error when no packaging rows exist; verify by deleting a category with zero packaging rows and confirming delete still succeeds

## 3. Admin delete UX feedback

- [x] 3.1 Update `wwwroot/js/category.js` `Delete()` success handler to branch on `data.success`: `success:true` → `dataTable.ajax.reload()` + `toastr.success`, `success:false` → `toastr.error(data.message)` with no reload; add `error:` callback for network/500; verify blocked delete shows `toastr.error` with count and table unchanged, successful delete reloads and shows success

## 4. Verification

- [x] 4.1 Run `dotnet build VaultShop.sln` and `dotnet test VaultShop.sln` (include new controller/unit tests for blocked vs allowed delete and packaging cascade); verify green and that scenario 1.1 reports only active count
- [x] 4.2 Run VPS orphan probes on both stores via `docker compose --env-file .platform.env -f docker-compose.platform.yml exec postgres ...` (Products orphan count + Packaging orphan count per design appendix); verify counts are 0 before and after the change, and document the `docker exec -U vaultshop_app` fallback
