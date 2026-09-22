## 1. Data — hours columns, migration, seed hardening

- [x] 1.1 Add `PostalAgency.Hours` with `[JsonPropertyName("horario")]` plus `OrderHeader.PickupAgencyHours NOT NULL DEFAULT 'no informa'`, create the EF migration, and verify `dotnet build VaultShop.sln` succeeds
- [x] 1.2 Extend `EnsurePostalAgencies` reseed compare-and-copy to include `Hours` (and every other copied field), add the stale-delete guard (abort + `LogCritical` when stale > 10% or > 200 rows), and verify a reseed updates an hours-only change (`changed > 0`) and seeded hours match the JSON sample
- [x] 1.3 Add the 2 cascade indexes (`ProvinceCode`, `(ProvinceCode, Locality)` covering Code/Name) and verify the migration applies cleanly
- [x] 1.4 Make `tools/refresh_sucursales.py` confirm-only (never delete, emit a parallel changelist) and carry `horario` through merges, and verify a dry run preserves hours and reports changes without modifying row counts

## 2. Cascade API — filtered endpoints with single eligibility predicate

- [x] 2.1 Expose `IsCandidate` as a SQL-translatable shared expression and reuse it in `GetByCode`, and verify existing candidate tests still pass
- [x] 2.2 Implement the 3 cascade GETs (`provinces`, `localities?provinceCode=`, `branches?provinceCode=&locality=`) as `IQueryable`-filtered queries using the shared predicate, and verify SQL filtering (no full-table load) plus locality scoping for homonym localities
- [x] 2.3 Add the fixed 24-province static list plus a CI test asserting seed province codes ⊆ list, and verify the test fails on an unknown code

## 3. Checkout — cascade replaces nearest search

- [x] 3.1 Build `_BranchCascadePicker` partial + VM + resx (es-AR/en-US) with `fieldset/legend`, `aria-live` status, native selects/radios, and client filter above 10 branches, and verify rendering in both cultures
- [x] 3.2 Wire the cascade into `Cart/Summary.cshtml` (province → locality → branch radios, submit gate, failed-POST rehydration of address + selection + candidates), persist the hours snapshot in `SummaryPOST` via server re-resolve, and verify the forged-code POST is rejected with no order created
- [x] 3.3 Delete the nearest-search path (`GetNearestAgencies`, geocoded search JS, `FindNearest` ranking usage) and verify `dotnet build` succeeds with no dead references

## 4. Admin — correction window and shipped freeze

- [x] 4.1 Reuse the cascade partial in `Admin/Order/Details.cshtml` for unshipped non-terminal pickup orders (snapshot line + correction hint), persist snapshot overwrite + log via `UpdateOrderDetail`, and verify correction works and sends no email
- [x] 4.2 Enforce the shipped freeze (`IsFrozen` guard in `UpdateOrderDetail` and `CancelOrder`, read-only flags in the view, delete `confirmShippedOrderUpdate` + dead resx keys), and verify shipped orders render with no mutating actions and forged POSTs change nothing
- [x] 4.3 Update tests (invert admin-shipped-edit to expect rejection, cover branch correction + forged code + cancel-on-shipped rejection) and verify `dotnet test VaultShop.sln` is green

## 5. Emails, summary, public copy

- [x] 5.1 Add the pickup branch block (name/address/hours with sentinel) + 5-business-day policy copy to buyer `OrderConfirmation` and `ShippingConfirmation` (in-transit-to-branch copy, static Correo landing link + plain-text tracking code, HTML-encoded tracking/carrier), and verify both cultures render correctly
- [x] 5.2 Add the hours line to the order summary HTML/PDF pickup block from persisted snapshot values, and verify the PDF matches the order data
- [x] 5.3 Add the long pickup-policy item to the FAQ/Terms public section (es-AR/en-US), and verify it renders in both cultures

## 6. Final verification

- [x] 6.1 Run `dotnet build VaultShop.sln` and `dotnet test VaultShop.sln` and verify green
- [x] 6.2 Browser-check es-AR + en-US: cascade select → place order → admin correct branch → ship → emails carry final branch with hours; shipped order fully read-only; mobile layout
