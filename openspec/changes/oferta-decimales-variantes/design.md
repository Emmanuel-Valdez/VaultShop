## Context

See proposal.md for why. The offer columns already exist. `ProductRepository.Update` copies fields by hand and skips the four offer fields, so a successful save leaves them null. Default culture is `es-AR`. New admin decimals use `type="number"`, which always posts a dot and renders a comma as blank. `_ValidationScriptsPartial` already rejects a dot under `es-AR`. Coupon and promotion selects use `GetEnumSelectList`, which prints the enum identifier. The detail picker runs `update()` on load; an empty select makes `resolve()` return null, and the unavailable message is shown whenever the match is not available. The favorite button submits the cart form, whose selects are `required`. Cart cleanup removes unavailable products only. `MaxUses` is a global `UsesCount` per order, not per customer.

## Goals / Non-Goals

**Goals:**

- One persistence fix, culture-correct decimal entry, localized selects, and a picker that only warns on a complete bad selection.
- Reuse the combination JSON already on the detail page. No new endpoint.
- Drop unsellable variant lines where unavailable products are already dropped.

**Non-Goals:**

- A scheduled cart sweeper.
- A per-customer coupon limit.
- Accepting both separators in both cultures.
- Changing stored enum values or offer validation rules.

## Decisions

1. Copy the four offer fields in `ProductRepository.Update`. Alternative: `_db.Products.Update(obj)` would also persist images and other fields the hand-copy deliberately ignores. Do not switch the whole method.
2. Decimal inputs become `type="text"` with `inputmode="decimal"`, matching `FinalRetailPrice`. The culture model binder plus the existing `es-AR` validator then do the separator rule. Do not add a custom binder that accepts both separators.
3. Build coupon type, promotion kind, and promotion scope options in the view from `IStringLocalizer` entries. Do not put `[Display]` on the enums; that does not switch with the request culture.
4. Favorite buttons get `formnovalidate` and post only `productId`, or sit in their own form. They must not wait on variant selects.
5. Picker: hide the unavailable message when any select is empty. Disable an option when no available combination contains it together with the other selected values. Data is the existing `#variant-combinations` JSON.
6. Listing filter: a product with at least one variant row is listed only if one of those rows is available. Products with no variant rows stay listed. Detail add-to-cart is hidden when no combination is available.
7. Variant cleanup calls the existing `ValidateVariantForProduct` from both `RemoveShoppingCartsOutdated` methods. Checkout creation keeps its current refusal as the race backstop. Do not add a background job.

## Risks / Trade-offs

- [Hand-copied update misses a future offer column] → The test asserts the four fields round-trip. A new column needs the same line.
- [Hiding a product with zero available variants surprises an admin who left the product "available"] → The product stays editable and favoritable. Only listing and purchase change.
- [Disabling options from client JSON can be bypassed] → Server validation on add-to-cart already rejects a bad variant. The script is UX only.
- [Two cleanup copies drift] → Both call the same variant validation. Do not invent a third cleaner.

## Migration Plan

No schema change. Deploy with the app. Rollback is a revert; unsaved offer edits made after the fix remain in the columns and are harmless if the old code ignores them again.

## Open Questions

None.
