## Context

See proposal.md for why. `BuildSummary` already accepts an optional `paymentMethod` (`ICheckoutService.cs:9`); the Summary GET simply never passes one, while SummaryPOST re-evaluations (lines 244, 254) and `CreateOrder` (lines 214-215) do. The evaluator's payment-discount block (`DiscountEvaluator.cs:85-104`) needs that method to produce `PaymentDiscountTotal`. The session request shape (`PaymentSessionLineItem` name/unit/count in `IPaymentSessionService.cs:17`) is unchanged — only the unit amounts differ. `ProrateCoupon` (`DiscountEvaluator.cs:241-269`) already solves the exact sub-problem (split a header-level amount across lines in whole cents that sum exactly, remainder on the largest line).

## Goals / Non-Goals

**Goals:**

- The charged session amount equals the persisted `OrderTotal` whenever a payment-method discount is active; byte-identical behavior when none is active.
- The shopper sees the applicable payment-method discount before Place Order, with server-computed numbers.
- FAQ documents the actual evaluator stacking, no behavior change.

**Non-Goals:**

- Changing evaluator stacking, persisted `OrderDetail`/header values, or payment providers.
- New evaluation paths: the AJAX endpoint reuses `BuildSummary` with the posted method.
- Hardcoded percents in views; picker text comes from active `PaymentMethodDiscount` promotions.

## Decisions

1. Prorate `PaymentDiscountTotal` across session line items only, mirroring `ProrateCoupon` (whole cents, remainder absorbed so lines sum exactly to `OrderTotal`). Alternative: a single discount line item — the session providers expect product lines and the order views show per-line prices; do not change the request shape.
2. AJAX re-evaluation endpoint calls the existing `BuildSummary(userId, wholesale, couponCode, paymentMethod)` and returns the discount row + total fragment/JSON. Do not invent a parallel calculator.
3. Picker announcement and selected-method note render server-side from active promotions; the client only toggles visibility on method change (same pattern as the existing bank-transfer details toggle).
4. FAQ Q11 states: specifics never stack (best-price-wins), payment-method discount stacks on top — matching `DiscountEvaluator.cs:27-44,86-104`. Spanish + English keys follow the existing `QuestionN`/`AnswerN` pattern.

## Risks / Trade-offs

- [Prorated session lines differ from persisted `OrderDetail` unit prices] → Session-only adjustment; the header total both agree on is `OrderTotal`. Provider receipts show the charged (correct) split.
- [Rounding leaves a cent gap] → Same remainder-on-largest technique as `ProrateCoupon`; a test asserts session lines sum exactly to `OrderTotal`.
- [AJAX re-evaluation without a method] → Falls back to current behavior (no payment discount row), identical to today's GET.

## Migration Plan

No schema change. Deploy with the app. Rollback is a revert; past orders already freeze the correct header totals.

## Open Questions

None.
