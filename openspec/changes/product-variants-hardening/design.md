# product-variants-hardening — Design

## Decisions

### 1. Conditional relative decrement (C1)

Replace the absolute `StockQuantity -= sum` + `Update` with a conditional relative write per product: `UPDATE Products SET StockQuantity = StockQuantity - total WHERE Id = id AND StockQuantity >= total`, treating 0 affected rows as `InsufficientStock`. The pre-read validation loop stays as a fast path; the conditional write is the real guard. Implemented via EF `ExecuteUpdate` with predicate (provider-agnostic: Npgsql + SQLite) inside the existing `ExecuteInTransaction`. The check constraint remains as backstop. No isolation-level change, no new locking.

### 2. Honest concurrency evidence (C2)

SQLite serializes writers and sequential contexts prove nothing about the race, so 6.1's form is replaced with deterministic tests of the guard itself: exact-stock checkout succeeds and leaves 0; a second checkout is rejected with stock intact and no order. The original `product-variants` task wording is superseded by this change's task 1.

### 3. Delete guard at the source (C3)

`DeleteVariant` refuses while any `ShoppingCart` line references the variant (`VariantReferencedByCart`, es/en resx), mirroring the existing guarded value-delete. Post-`SetNull` the checkout cannot distinguish a deleted-variant line from a legacy variant-less line, so guarding deletion is the only deterministic close. No availability re-check at checkout: disabled-variant lines must remain purchasable ("existing carts remain intact").

### 4. Admin ownership

`DeleteValue`, `SetAvailability`, `DeleteVariant` verify the target row belongs to the posted `productId`, failing with `VariantInvalid` otherwise. No controller signature changes.

### 5. Cart labels

`CartController.Index`/`Summary` include the `Variant` navigation; `Index.cshtml` and `Summary.cshtml` render `VariantLabel`-equivalent under the product name when present (same `<small class="text-muted">` pattern as order surfaces). Labels are built with the existing `BuildVariantLabel`.

### 6. Label column

`ALTER COLUMN VariantLabel 500 → 2000` plus `[MaxLength(2000)]` on the model so model/snapshot/migration agree and overlong values fail fast at validation instead of inside the checkout catch-all.

### 7. Batching + tests

`GenerateCombinations`: build all rows in memory, one `SaveChanges` (tracked-add conflict risk is nil — fresh rows). No hard cap (trusted admin role; count already reported). Tests: true type-coverage case via a directly-inserted partial-coverage variant; `ProductVariantController` POST success/error/cross-product paths; Minus/Remove behavior.
