## Context

See proposal.md for why. The three admin controllers each carry an identical private pair (`OfferToUtc` via `ConvertTimeToUtc(..., TimeZoneInfo.Local)`, `OfferToLocal` via `ConvertTimeFromUtc`). `StorefrontPricingService.cs:54` and `CheckoutService.cs:55` evaluate with `DateTime.UtcNow`; `DiscountEvaluator.InWindow` is inclusive (`now >= from && now <= to`). The kept `_DiscountPrice.cshtml` renders `OfferEndUtc` raw with `<time datetime="...Z">` plus the `OfferEnd` resx keys (`_DiscountPrice.{es,en}.resx`); `ProductDisplayPrice` already carries `DateTime? OfferEndUtc`, populated from `p.SaleToUtc` only for a direct offer motive.

## Goals / Non-Goals

**Goals:**

- One shared Argentina-time helper replacing all three private duplicates at the same call sites.
- Storefront deadline shows Argentina wall time; machine-readable attribute keeps the UTC instant.
- No data migration; no evaluator change.

**Non-Goals:**

- Per-admin or per-browser timezones; the admin operates in Argentina time, fixed.
- Changing stored values, evaluation semantics, or the inclusive window rule.
- Touching `oferta-decimales-variantes` tasks.

## Decisions

1. Single static helper pinned to IANA `America/Argentina/Buenos_Aires` with fallback to Windows `Argentina Standard Time` on `TimeZoneNotFoundException`. Alternative: `TimeZoneInfo.Local` keeps the +3h shift in Docker; a configurable zone adds config for a value that never changes.
2. Replace the three private pairs in place (same method names at the call sites or direct helper calls). Do not add an injectable service; conversion is pure and the call sites are static.
3. The partial converts `OfferEndUtc` to Argentina time for the visible `OfferEnd` text; `datetime` keeps the UTC instant (`yyyy-MM-ddTHH:mmZ`). Do not shift the stored value.
4. No migration. Deploy with the app; rollback is a revert.

## Risks / Trade-offs

- [Windows vs Linux zone ids] → Resolve IANA first, fall back to the Windows id; a test asserts resolution succeeds on the current platform.
- [Windows saved under the old rule keep their stored instants] → Windows entered before this change keep whatever UTC was persisted; going forward entry and display agree. No backfill per scope.
- [Unspecified `DateTimeKind` from form posts] → Helper treats input as `Unspecified` in Argentina time, same as the old code treated it as server-local.

## Migration Plan

No schema change. Deploy with the app. Rollback is a revert; values saved after the fix remain valid UTC instants.

## Open Questions

None.
