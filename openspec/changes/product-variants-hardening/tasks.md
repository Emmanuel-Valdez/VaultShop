## 1. Conditional stock decrement (C1 + honest C2)

- [x] 1.1 Replace absolute decrement with conditional relative `ExecuteUpdate` per product (`StockQuantity >= total`, 0 rows → `InsufficientStock`), verified by exact-stock checkout leaving 0 and a follow-up checkout rejected with stock intact
- [x] 1.2 Supersede old 6.1 evidence wording: deterministic guard tests carry the race evidence (they fail if the conditional write reverts to absolute); the old sequential test is renamed to what it actually proves, and the SQLite-can't-test-races limitation is recorded in `product-variants/tasks.md` 6.1

## 2. Delete guard at the source (C3)

- [x] 2.1 `DeleteVariant` refuses while cart lines reference the variant (`VariantReferencedByCart` + es/en resx), verified by blocked-delete and clean-delete tests
- [x] 2.2 `DeleteValue`/`SetAvailability`/`DeleteVariant` verify target belongs to posted `productId` (`VariantInvalid` on cross-product ids), verified by cross-product rejection tests

## 3. Labels, column, batching

- [x] 3.1 Cart page + checkout summary render the variant label per line, verified by HTTP test with two variants showing distinguishable labels
- [x] 3.2 `VariantLabel` column 500 → 2000 with matching `[MaxLength]` on the model, verified by migration + overlong label persisting instead of misreporting stock
- [x] 3.3 `GenerateCombinations` batches to a single `SaveChanges`, verified by existing 8-row test still passing plus a count assertion on save behavior

## 4. Coverage gaps

- [x] 4.1 True type-coverage test via directly-inserted partial-coverage variant, verified by `VariantInvalid` from the coverage branch (not ownership)
- [x] 4.2 `ProductVariantController` POST success/error/cross-product tests, verified by covering all five actions
- [x] 4.3 Minus/Remove behavior tests (decrement, remove-at-1, sibling untouched), verified by new facts
- [x] 4.4 Full suite green (`dotnet test VaultShop.sln`) with no regressions
