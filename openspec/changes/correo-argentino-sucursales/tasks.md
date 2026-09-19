## 1. Snapshot — acquisition and data

- [x] 1.1 Spike: probe `wsFacade.php` (and site sucursales endpoint) for a JSON returning branches with lat/lon and verify schema matches `PostalAgency` and verify the national count is ~3,300
- [x] 1.2 Fallback: generate `sucursales.json` with coordinates — ingest national gist (no coords) and batch-geocode via Georef `/direcciones` lotes until every row has `lat/lon`, and verify no row has null coords and file is checked in under `data/` or `VaultShop.DataAccess/SeedData/`
- [x] 1.3 Add `PostalAgency` entity (Code PK, Name, Street, Number, Locality, City, Province, ProvinceCode, PostalCode, Latitude, Longitude) and verify `dotnet build` succeeds
- [x] 1.4 Add EF migration + idempotent seed (upsert by Code) for `PostalAgency` and verify `dotnet ef database update` locally seeds ~3,300 rows and re-running seed does not duplicate
- [x] 1.5 Add `PostalAgency.LastVerifiedUtc` (nullable) + `Source` ("correo"|"gist") + migration `AddPostalAgencyVerification`, extend seed upsert to those fields, and verify `dotnet build` succeeds
- [x] 1.6 Write `tools/refresh_sucursales.py` (stdlib-only, re-runnable monthly): scrape `wsFacade.php` `localidadesconsucursales` + `sucursales` per province, parse `L.marker([lat,lng])` + code from card `rel`, merge over `sucursales.json` by Code (official coords + `LastVerifiedUtc=now` on match, keep gist-only rows as closure candidates), and verify report shows confirmed/unconfirmed/new counts
- [x] 1.7 Second-pass match by address in `tools/refresh_sucursales.py`
- [x] 1.8 Capture services + ingest UP: extend `tools/refresh_sucursales.py` to store card `rel` service ids per branch and ingest UNIDAD POSTAL points (skip AGENCIA-labeled pins duplication) with stable synthetic codes `UP-{provinceCode}-{hash6}` over name|locality|street and `Kind="UP"`; add `PostalAgency.Services` (string) + `Kind` (string) + migration, extend seed upsert, re-run full refresh, and verify CABA has ~49 SUC + ~265 UP rows and every candidate row carries service `40` where the site lists it: for gist-only rows, match site entries on (normalized street + number + locality) within the same province — on one-to-one match rename to the site name (site wins), set official coords + `LastVerifiedUtc=now` + `source="correo"`, log renames; skip ambiguous matches with warning — and verify report shows rename count and remaining unverified count

## 2. Order model — agency snapshot

- [x] 2.1 Add `OrderHeader` columns `DeliveryType` (string), `PickupAgencyCode`, `PickupAgencyName`, `PickupAgencyAddress` (all nullable for pre-change orders) and verify migration creates them
- [x] 2.2 Update `ApplicationDbContext` / model snapshot and verify `dotnet build` succeeds

## 3. Services — geocoding and nearest-5

- [x] 3.1 Implement `GeorefAddressService` (`HttpClient`, `GeorefOptions:BaseUrl`, 5s timeout) calling `georef/api/direcciones?direccion=&provincia=&localidad=&max=1` and verify it returns `lat/lon` for a known Mendoza address and `null` on bad address / non-2xx
- [x] 3.2 Implement `NearestAgencyService` (haversine, province-filtered, top 5, distanceKm, verified `Source="correo"` + service-`40` filter, both `Kind`s) and verify unit test `NearestAgencyServiceTests` passes for ranking, tie, <5-in-province, unverified-exclusion, and no-40-exclusion cases
- [x] 3.3 Register services in DI (`Program.cs`) and verify checkout still resolves `CartController`

## 4. Checkout — branch search and selection

- [x] 4.1 Add `CartController.GetNearestAgencies` (GET or POST with antiforgery) returning JSON `[{code,name,address,locality,province,distanceKm}]` using `GeorefAddressService` + `NearestAgencyService`, and verify manual call returns 5 for a Mendoza address
- [x] 4.2 Update `Cart/Summary.cshtml` — add "Retiro en sucursal — Envío gratis" block with "Buscar sucursales cercanas" button and required radio list of 5 candidates (hidden `PickupAgencyCode` + client snapshot), and verify the picker appears and enforces selection
- [x] 4.3 Update `CartController.SummaryPOST` to validate a branch is selected, re-resolve the snapshot from `PostalAgency` by code (never trust client name/address), persist `DeliveryType=S` + snapshot on `OrderHeader`, and verify an order created with a picked branch stores the denormalized snapshot

## 5. Admin and order history

- [ ] 5.1 Show agency snapshot on `Admin/Order/Details` (and customer order history) when `DeliveryType=S`, and verify pre-change orders render no agency block without error

## 6. Localization and copy

- [ ] 6.1 Add `es-AR`/`en-US` resx entries for the branch block, free-shipping copy, search button, validation messages, and agency fields, and verify both cultures render correctly

## 7. Tests and verification

- [ ] 7.1 Add `VaultShop.Tests` coverage: `GeorefAddressService` parsing (mock `HttpMessageHandler`) + `NearestAgencyService` haversine ordering + `SummaryPOST` rejects missing agency, and verify `dotnet test` passes
- [ ] 7.2 Run full verification `dotnet build` and `dotnet test` and verify green
