## 1. Controller — preserve input + rehydrate candidates

- [x] 1.1 Add `RestorePostedHeaderAndCandidates` helper in `CartController` (overlay posted name/phone/street/city/state/postal/payment/picked-code onto rebuilt VM + geocode + nearest-5 into `ViewData["AgencyCandidates"]`) and verify `dotnet build VaultShop.sln` succeeds
- [x] 1.2 Wire helper into both `SummaryPOST` failure branches (`ModelState` invalid + agency-null) and verify a failed POST re-renders with typed address intact (manual or HTTP test)
- [x] 1.3 Guard rehydration on empty street/state (skip geocode, province-fallback only, never throw) and verify empty-address POST still renders without error

## 2. View — server candidates + search hardening + submit gate

- [x] 2.1 Render `ViewData["AgencyCandidates"]` server-side in `Summary.cshtml` with posted code pre-checked (same radio name/markup as JS path) and verify failed POST shows candidates with selection
- [x] 2.2 Harden search JS (require street + state, disable button + `aria-disabled` while in flight, AbortController for stale requests, non-OK/network → `NoBranchesFound` copy) and verify double-click issues one fetch and failures show copy
- [x] 2.3 Gate `Place Order` on selection (disabled until a radio is checked, enabled on `change`, JS-sets-initial-disabled so no-JS stays enabled) and verify initial load disabled → pick enables → new search keeps gate
- [x] 2.4 Add `EnterAddressBeforeSearch` key to `Summary.{es,en}.resx` and verify both cultures render it

## 3. Tests and verification

- [x] 3.1 Add `SummaryPost_WithoutAgency_PreservesAddressAndRehidesCandidates` HTTP test (typed address + payment survive, candidates re-listed, no order) and verify it passes
- [x] 3.2 Add `SummaryPost_WithInvalidAgencyCode_RehidesCandidates` HTTP test (forged code → no order, candidates re-listed, error shown) and verify it passes
- [x] 3.3 Run `dotnet build VaultShop.sln` and `dotnet test VaultShop.sln` and verify green
- [x] 3.4 Browser-check es-AR + en-US: search → pick → submit-empty path, empty-address search guard, disabled submit gate, mobile layout
