## P0 — Money correctness (single diff, evaluator + checkout)

- [x] P0.1 BxGy multi-group accumulation in `DiscountEvaluator.EvaluateBxGy` (per-promo `+=` per granted unit, max across promos outside the group loop), verify `BxGy_MultiQuantitySameSku_GrantsDiscountPerGroup` (6 units @100, 2x1 → discount 300) fails before / passes after.
- [x] P0.2 Coupon per-line preservation in `DiscountEvaluator.ProrateCoupon` (`Max(existing, share)`, keep winning motive; header = sum of winners), verify `Coupon_DoesNotShrinkALargerPerLineDiscount` (offer-5000 line + flat line + 10% coupon → offer line keeps 5000) fails before / passes after.
- [x] P0.3 Line-price rounding in `CheckoutService.EvaluateCart` (round unit to cents, absorb remainder in last line of same product), verify `FrozenLines_SumToHeaderTotal` (200 over 3 units → whole-cent prices summing exactly to header) fails before / passes after.
- [x] P0.4 Full evaluator + checkout suites green with no-discount fallback unchanged, verify `dotnet test` shows 0 regressions vs P0 start.

## P1 — Persistence, races, parity

- [x] P1.1 Persist `OrderHeader.PaymentDiscountMotive` (mapped `varchar(120)` column + migration, remove `[NotMapped]`), verify migration applies on a scratch database and an order created with a transfer discount renders its frozen motive in admin details, customer history, email, and PDF after the promotion is renamed.
- [x] P1.2 Atomic coupon increment (`ICouponRepository.TryIncrementUses` conditional `UPDATE`, loser takes existing `couponDropped` path in `CheckoutService.CreateOrder`), verify `CreateOrder_CouponLostRaceAtCreation` (bump `UsesCount` in a second context between evaluation and creation → `CouponDroppedAtCreation`, no double use) covers the previously unexecuted branch.
- [x] P1.3 Shared `IsGrantableBxGy` helper used by both evaluator and `StorefrontPricingService.FindCoveringBxGy` (active + window + wholesale + quantities + pct + scope), verify inactive and zero-quantity promotions grant nothing AND show no badge.
- [x] P1.4 Admin BxGy wording ("Llevá X, pagá Y (grupo X+Y)") + en/es resx, verify Upsert renders explicit labels in both cultures.
- [x] P1.5 Frozen-breakdown survival test (`Order_FrozenBreakdown_SurvivesPromotionAndCouponDeletion` — delete promo/coupon rows, re-read order), verify motives and amounts intact.

## P2 — Rendering proof (after logic is fixed)

- [x] P2.1 Badge + strikethrough rendering test in both cultures (offer badge/`<s>` and BxGy badge via test host, `Accept-Language: en-US` + default es-AR), verify markup asserts pass where data-only tests stood before.
- [x] P2.2 Full suite green (`dotnet test VaultShop.sln`), `dotnet ef migrations script` reviewed, `openspec validate --change descuentos-correctitud-pagos` clean.
