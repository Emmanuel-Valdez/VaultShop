## Context

See proposal.md — Why. The shipped `descuentos-promociones` change (unarchived, working tree dirty) owns `DiscountEvaluator` (pure, shared by storefront/cart/checkout), `StorefrontPricingService.FindCoveringBxGy` (reimplemented guards), `CheckoutService.EvaluateCart`/`CreateOrder` (unrounded `EffectiveTotal / Count`, `UsesCount++` read-modify-write), and `OrderHeader.PaymentDiscountMotive` (`[NotMapped]`). Constraints: no new abstractions beyond one shared helper; specs in this change rule over the shipped wording where they conflict (BxGy naming, coupon-wholesale); migration must apply on scratch PostgreSQL.

## Goals / Non-Goals

**Goals:**
- Every money path exact: multi-group BxGy, per-line coupon preservation, cent-exact frozen lines, atomic coupon uses.
- Frozen trace complete: payment motive persisted and rendered on all four order surfaces.
- Badge parity: storefront never promises what the cart would not grant.

**Non-Goals:**
- No stacking of specific discounts (unchanged); no `Coupon.IncludeWholesale` flag (decision: coupons apply to wholesale, spec amended, zero code); no AFIP/ARCA impact; no loyalty/gift cards.

## Decisions

- **BxGy: accumulate per promotion, max across promotions.** Inside one promotion's group loop, `+=` the rounded unit benefit into a per-line accumulator (tracking winning promo per line); after all promotions, keep the existing per-line max against offer/collection. Alternative (multiply groups × unit price) rejected — units across mixed-SKU pools have different prices, so per-unit accumulation is the only exact form. Preserves best-price-wins per line.
- **Coupon: `Max` per line, header = sum of winners.** `ProrateCoupon` writes `lineDiscounts[i] = Max(existing, share)` and keeps the pre-coupon motive where the existing discount wins; the nominal `couponTotal` survives only as the prorate basis, and `SpecificDiscountTotal` (sum of lines) becomes the frozen coupon figure. Alternative (exact-share prorate) rejected — it degrades lines holding larger discounts and corrupts partial refunds. The "shares sum exactly" remainder rule stays, applied to coupon-winning lines.
- **Rounding: round unit, absorb remainder in last line.** `EvaluateCart` rounds each cart `Price` to cents, then adjusts the last line of the same product by the signed remainder so `Price × Count` sums to `EffectiveTotal`. Alternative (fractional persistence) impossible — `numeric(18,2)`. Header `OrderTotal` already equals `evaluation.Total`; lines are fitted to it, not vice versa.
- **Atomic coupon increment via conditional `UPDATE`.** New `ICouponRepository.TryIncrementUses(id, maxUses)` → `ExecuteUpdate(UsesCount+1) WHERE Id AND (MaxUses IS NULL OR UsesCount < MaxUses)`; rows==0 means lost race → existing `couponDropped` re-evaluation path. Reuses the `DecrementStockIfSufficient` pattern (`ProductRepository.cs:52-57`) instead of a `[Timestamp]` token — no model churn, provider-agnostic, same as the stock race fix. Alternative (tracked `++` + re-check) rejected — READ COMMITTED loses the race by design.
- **Persist payment motive: plain nullable column.** `PaymentDiscountMotive varchar(120)` mapped (remove `[NotMapped]`), backfilled NULL for existing rows (motive is display-only; amounts already frozen). `StampDiscountHeader` and `OrderSummaryService` mappings already flow the value — no view/email/PDF logic changes beyond what 6.1 built.
- **Badge parity: one shared `IsGrantableBxGy` helper** on the evaluator side (active + window + wholesale opt-in + `BuyQty/GetQty > 0` + pct > 0 + scope), used by both `EvaluateBxGy` and `FindCoveringBxGy`. Alternative (duplicate guards in badge) rejected — duplication caused this bug.
- **BxGy naming: code wording wins.** `2x1 = Buy 1 Get 1`, `3x2 = Buy 3 Get 2`; admin labels become "Llevá X, pagá Y (grupo X+Y)"; specs corrected. Formula `groupSize = Buy + Get` untouched.

## Risks / Trade-offs

- [Risk] Coupon `Max` changes frozen totals for mixed carts vs shipped behavior → Mitigation: new behavior is the spec; regression test pins the mixed scenario; past orders untouched (frozen).
- [Risk] Remainder-absorption line looks odd (e.g. 66.66 ×2 + 66.68) → Mitigation: 1-cent skew on one line, totals exact; matches standard invoicing practice.
- [Risk] Conditional coupon `UPDATE` behaves differently on SQLite/InMemory in tests → Mitigation: same fallback consideration as `DecrementStockIfSufficient`; race test uses real EF SQLite path like stock tests do.
- [Risk] Migration on live DB with existing discounted orders → Mitigation: nullable column, no backfill of motives (amounts intact), backwards-compatible reads.
- [Trade-off] Header coupon figure no longer equals nominal % × subtotal in mixed carts → documented in spec as sum-of-winners; cart UI shows per-line motives so the shopper sees why.

## Migration Plan

1. Ship code + migration together; `dotnet ef migrations script` reviewed for nullable column, no data loss.
2. Verify migration applies on scratch PostgreSQL (covers the 1.2 blind spot retroactively).
3. Rollback: revert change; new column ignored by old code (nullable, unread).

## Open Questions

- None — coupon-wholesale and BxGy naming decisions are recorded above and in specs.
