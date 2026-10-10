## 1. Shared timezone helper

- [ ] 1.1 Create the shared helper (e.g. `OfferTimeZone.ToUtc`/`ToLocal`) pinned to `America/Argentina/Buenos_Aires` with `Argentina Standard Time` fallback, and verify a round-trip unit test (including a midnight/day-boundary case) plus a zone-resolution test passing on the current platform.
- [ ] 1.2 Replace the private `OfferToUtc`/`OfferToLocal` pairs in `ProductController`, `CouponController`, and `PromotionController` with the helper, and verify saving a window then reopening the form shows the same Argentina wall time that was entered.

## 2. Storefront deadline display

- [ ] 2.1 Render `OfferEndUtc` in `_DiscountPrice.cshtml` converted to Argentina time (visible `OfferEnd` text in ART, `<time datetime>` with the UTC instant), and verify a partial test with a date shows ART and a test without a date renders nothing extra.

## 3. Verification

- [ ] 3.1 Run the directed tests plus `openspec validate oferta-huso-horario-ar` and verify both are clean.
