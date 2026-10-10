## Why

The admin enters offer, coupon, and promotion windows in browser wall time (Argentina, UTC-3), but the three admin controllers interpret that wall time as server-local time (`TimeZoneInfo.Local`; UTC in Docker). The identical private converter is triplicated in `ProductController.cs:218-225`, `CouponController.cs:101-108`, and `PromotionController.cs:160-167`, while evaluation runs on `DateTime.UtcNow` with an inclusive window (`DiscountEvaluator.InWindow`). Result: windows shift +3h and offers "never activate". The kept `_DiscountPrice.cshtml` deadline display (see `oferta-decimales-variantes` working tree) has the mirror defect: it renders the raw UTC instant instead of Argentina time.

## What Changes

- One shared helper (e.g. `OfferTimeZone.ToUtc`/`ToLocal`) pinned to `America/Argentina/Buenos_Aires`, with fallback to the Windows id `Argentina Standard Time` (dev is Windows, prod is Linux). It replaces the three triplicated private converters. Storage and evaluation stay in UTC; only input interpretation and admin form display change.
- The `_DiscountPrice.cshtml` offer-deadline render uses the same helper: the `<time datetime>` attribute keeps the correct UTC instant and the visible text shows Argentina time.
- No migration: stored values are already UTC.
- Out of scope: anything in `oferta-decimales-variantes` except adopting its four kept deadline-display files as the starting point.

## Capabilities

### New Capabilities

- None.

### Modified Capabilities

- `pricing/discounts`: admin-entered windows are interpreted as Argentina time; stored/evaluated in UTC; the storefront deadline renders in Argentina time.

## Impact

- New helper (e.g. `VaultShop.Web/Services/Pricing/OfferTimeZone.cs`); `Areas/Admin/Controllers/ProductController.cs`, `CouponController.cs`, `PromotionController.cs` drop their private converters for it.
- `Views/Shared/_DiscountPrice.cshtml` converts `OfferEndUtc` to Argentina time for display.
- No model, evaluator, or schema change. No migration.
