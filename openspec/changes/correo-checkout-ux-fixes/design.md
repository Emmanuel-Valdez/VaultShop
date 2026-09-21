## Context

`CartController.SummaryPOST` (`VaultShop.Web/Areas/Customer/Controllers/CartController.cs:142`) currently returns a fresh `CheckoutService.BuildSummary` VM on both failure paths (`ModelState` invalid at :152 and agency-null at :160), discarding the posted `ShoppingCartVM.OrderHeader`. The candidate list in `Summary.cshtml:89-100` is JS-only with no server render, so it vanishes on postback. Search JS at `Summary.cshtml:173-215` fires unconditionally, never disables, and swallows non-OK as empty. See proposal.md for motivation.

## Goals / Non-Goals

**Goals:**
- Failure-path POST preserves shopper input and re-shows candidates with selection intact.
- Search button behaves (guard, disable, abort, error copy) with existing localized strings.
- Submit gate is progressive enhancement only; server stays authoritative.

**Non-Goals:**
- No new endpoint, no postal-code plumbing (Georef ignores it), no map, no pricing/toggle changes.
- No candidate caching, no pagination beyond 5, no admin changes.

## Decisions

**Overlay posted header onto rebuilt VM (not reuse of posted VM)**
- On failure, take `summaryResult.ShoppingCartVM` (fresh cart lines + totals + user defaults) and overwrite its `OrderHeader` scalar fields from the posted `ShoppingCartVM.OrderHeader` (name, phone, street, city, state, postal, payment, picked code). Keeps totals/cart authoritative, input preserved.
- Alternative rejected: return the posted VM directly — cart lines/totals would be stale/untrusted.
- Private helper `RestorePostedHeaderAndCandidates(...)` shared by both failure branches to avoid duplication.

**Server-side rehydration via existing services, no new endpoint**
- Failure path calls `IGeorefAddressService.GeocodeAsync(posted street, state, city)` + `INearestAgencyService.FindNearest(...)` and passes candidates via `ViewData["AgencyCandidates"]` (+ posted code for `checked`). `Summary.cshtml` renders them server-side when present, JS takes over afterwards for new searches.
- Alternative rejected: TempData JSON round-trip or AJAX re-fetch on load — extra hop, fails with no-JS.
- Empty posted street/state → skip geocode, still render province fallback list (same service semantics).

**Submit gate as JS-only `disabled` toggle**
- `Place Order` starts disabled when no radio checked (server renders `disabled` when no pre-checked candidate); a delegated `change` listener on `#agencyCandidates` enables on check. No-JS path unaffected (button enabled, server validates).
- Alternative rejected: `required` alone — already present but gives no pre-search affordance.

**Search hardening with AbortController + minimal new copy**
- Guard `street && state` before fetch; show new `EnterAddressBeforeSearch` key (1 key × 2 cultures) else reuse `NoBranchesFound` for empty/fetch-fail. Disable button + `aria-disabled`, `AbortController` cancels stale request, `finally` re-enables.
- Alternative rejected: postal-code field plumbing — service signature has no postal code; YAGNI.

## Risks / Trade-offs

- **Extra Georef call on failed POST** → slower error render; mitigation: same 5s timeout, failure falls back to province list, never throws.
- **Server-rendered + JS-rendered list divergence** → mitigation: single radio `name="OrderHeader.PickupAgencyCode"` and markup in both paths; JS clears server list on new search.
- **Disabled submit confusing with no-JS** → mitigation: only render `disabled` when JS enhancement marker present (`<html class="js">` toggle or set disabled via JS on load, not in HTML).

## Migration Plan

1. Land behind no flag — additive failure-path + view-only changes; pre-change orders untouched.
2. Verify: `dotnet build`, `dotnet test` (new cases green), manual es-AR/en-US browser pass (search → pick → submit-empty → back shows input + candidates).
3. Rollback: revert the two files + resx; no data migration involved.

## Open Questions

- None. Copy for `EnterAddressBeforeSearch` wording settled at implementation review.
