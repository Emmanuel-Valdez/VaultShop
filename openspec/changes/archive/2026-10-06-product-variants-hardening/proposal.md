# product-variants-hardening — Proposal

## Why

Post-implementation review of `product-variants` (21/21, complete) found spec violations and real defects that the original tasks left behind:

1. **Checkout can oversell under real concurrency.** `CheckoutService` reads `StockQuantity`, validates, then writes an absolute value under READ COMMITTED. Two concurrent last-unit checkouts both pass validation; the second overwrites with a non-negative value, so `CK_Products_StockQuantity_NonNegative` never fires. The stock spec ("at most one order succeeds") is not honored.
2. **Task 6.1's test is sequential**, not concurrent — it passes even without a transaction.
3. **Checkout never re-validates variants.** `ON DELETE SET NULL` on the variant FKs plus an unguarded `DeleteVariant` lets a deleted variant silently convert a cart line into a base-product purchase on a variants-only product — forbidden by the catalog spec.
4. **Admin mutations ignore the posted `productId`** (cross-product value/variant deletes with a success toast).
5. **Cart page and checkout summary show no variant label** — sibling variant lines render identically.
6. **`VariantLabel` (varchar 500) can overflow** on long type/value names; the `DbUpdateException` catch-all would misreport it as insufficient stock. The model also lacks the length annotation.
7. **Coverage gaps**: type-coverage guard never actually executed by tests, `ProductVariantController` 0% covered, Minus/Remove untested, `GenerateCombinations` does 2 `SaveChanges` per row with no batching.

## Scope

Fix items 1–7 as agreed (Critical + High). Explicitly out of scope: PDF text-content assertions (needs a new dependency), `Migrate()`-on-SQLite test for the migration (investigate separately), hard cap on combination counts (admin is a trusted role; batching suffices), deleting the `Product.Variants` navigation seam (documented extension point per design decision 6).
