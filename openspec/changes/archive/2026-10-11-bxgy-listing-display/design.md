## Context

See proposal.md — Why. `StorefrontPricingService.GetDisplayPrices` currently sends the **entire listing as one** `DiscountEvaluationRequest`, every product as a line with `Count = 1`. `DiscountEvaluator.EvaluateBxGy` pools all eligible lines into a single mixed unit pool when `SameSkuOnly = false`, so a listing forms phantom BxGy groups across unrelated products and grants the combo discount to the cheapest listed unit. `Details` passes a single product and is unaffected. The `_DiscountPrice.cshtml` partial already renders BxGy as badge-with-no-strikethrough when `EffectivePrice == BasePrice`, so no view changes are required.

## Goals / Non-Goals

**Goals:**
- Pre-cart listings (Home, featured, Search, Favorites) show the genuine single-unit price; BxGy appears as a badge only.
- Keep direct offer and collection percentage offers discounted on listings, unchanged.
- Preserve cart/checkout BxGy evaluation exactly (`SameSkuOnly=false` mixed-product pooling is intended).
- Single locus of change; no new evaluator API.

**Non-Goals:**
- Changing `DiscountEvaluator` semantics (cart/checkout equivalence).
- Spreading the cart's $0 per-line rendering (the whole group discount is attributed to the cheapest unit's line) — totals are correct and that behavior is a separate presentation concern.

## Decisions

- **Evaluate each product in isolation in `GetDisplayPrices`.** Replace the single batched `Evaluate` with one `Evaluate` call per product whose request contains a single line (`Count = 1`). BxGy then can never form a group (a lone unit is always fewer than `BuyQty + GetQty ≥ 2`), so its `DiscountAmount` is 0 and the badge is driven by the existing `FindCoveringBxGy` coverage check. Direct offers (`OfferPayable`) and collection offers (`EvaluateCollectionOff`) are per-line by nature and still apply to the isolated unit. Alternative (a `displayMode` flag stored on the request that suppresses `EvaluateBxGy`) was rejected: it adds a mode knob that must be threaded through the evaluator and tested, while per-product isolation needs no new API and naturally yields the badge-via-coverage path already coded in the service.
- **Keep the promotion query shared.** `_unitOfWork.Promotion.GetAll(...)` is called once and reused across product evaluations, so the change adds no extra database round trips.
- **Drop the now-unneeded `byProduct` grouping.** The current code groups evaluated lines by `ProductId` to tolerate duplicate lines; with one isolated line per product, the single returned line maps directly (`line.LineKey == p.Id`), so the grouping is removed.
- **Performance note (`ponytail`).** Per-product evaluation is `O(P × promotions)` on the in-memory promotion list — negligible for `PAGER_SIZE=12` pages and a handful of active promotions. A dedicated display path optimizes the common single-offer lookup; revisit only if listings grow orders of magnitude.

## Risks / Trade-offs

- [Risk] A BxGy badge on a listing might still imply a discount that only materializes in the cart. → Mitigation: unchanged badge behavior (the same «grantable for this viewer» rule) plus the spec note that BxGy arrives in the cart; the FAQ Q11-style messaging already exists.
- [Risk] Regressing collection offers that are genuinely per-unit. → Mitigation: covered by a new multi-product test asserting collection % strikethrough still renders beside badge-only BxGy products.
- [Trade-off] N small allocations per listing instead of one — accepted for correctness; measure only if profiling shows a bottleneck.

## Migration Plan

Single commit: `StorefrontPricingService.GetDisplayPrices` isolation + widened `StorefrontPricingServiceTests`. No data migration, no config, no dependency. Rollback = revert the commit.

## Open Questions

None.