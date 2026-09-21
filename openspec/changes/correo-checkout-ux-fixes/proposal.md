## Why

The Correo Argentino branch pickup works end-to-end (search → 5 nearest → snapshot on order), but the `Cart/Summary` checkout UX loses user work on the failure paths: a submit without a branch wipes the typed address and payment choice, the JS-only candidate list vanishes after a failed POST, and the search button has no guard against empty addresses, double-clicks, or fetch failures. Fix now while the parent `correo-argentino-sucursales` change is still unarchived, before shoppers hit these paths in production.

## What Changes

- `SummaryPOST` preserves the posted `OrderHeader` (name, phone, address, city, state, postal code, payment method, picked code) when re-rendering after agency or model validation failures, instead of returning a fresh `BuildSummary` VM.
- Failed POST re-hydrates the branch candidate list server-side (re-runs geocode + nearest-5 with posted address) and re-marks the posted selection, so the picker survives postback.
- `Summary.cshtml` search flow hardens: requires street + state before fetch, disables the button with `aria-disabled` + localized "searching" status while in flight, surfaces fetch/non-OK errors via the existing `NoBranchesFound` copy, and aborts stale requests.
- `Place Order` submit is gated client-side until a branch radio is checked (server validation stays authoritative); the gate degrades gracefully with no-JS (server error still shows).
- No pricing, routing, data-model, or seed changes. No new endpoints. No postal-code param (Georef service ignores it — YAGNI).

## Capabilities

### New Capabilities
- `shipping/correo-pickup-ux`: checkout branch-picker resilience — form preservation, candidate rehydration, search-button hardening, submit gating.

### Modified Capabilities
- None

## Impact

- `VaultShop.Web/Areas/Customer/Controllers/CartController.cs` — `SummaryPOST` failure paths (both `ModelState` invalid and agency-null branches); new private helper to overlay posted header + rehydrate candidates via existing `IGeorefAddressService`/`INearestAgencyService`.
- `VaultShop.Web/Areas/Customer/Views/Cart/Summary.cshtml` — search JS (guard, disable, AbortController, error copy), submit gate JS, server-rendered candidate list partial or `ViewBag` rehydration.
- `VaultShop.Web/Resources/Areas/Customer/Views/Cart/Summary.{es,en}.resx` — at most 1-2 new keys (e.g. `EnterAddressBeforeSearch`); reuse `SearchingBranches`/`NoBranchesFound`.
- `VaultShop.Tests/CartCheckoutHttpTests.cs` — new cases: failed POST preserves address, failed POST re-shows candidates + selection.
- No migration, no `PostalAgency`/`OrderHeader` schema change, no pricing change.
