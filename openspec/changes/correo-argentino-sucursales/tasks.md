## 1. Snapshot — acquisition and data

- [x] 1.1 Spike: probe `wsFacade.php` (and site sucursales endpoint) for a JSON returning branches with lat/lon and verify schema matches `PostalAgency` and verify the national count is ~3,300
- [x] 1.2 Fallback: generate `sucursales.json` with coordinates — ingest national gist (no coords) and batch-geocode via Georef `/direcciones` lotes until every row has `lat/lon`, and verify no row has null coords and file is checked in under `data/` or `VaultShop.DataAccess/SeedData/`
- [x] 1.3 Add `PostalAgency` entity (Code PK, Name, Street, Number, Locality, City, Province, ProvinceCode, PostalCode, Latitude, Longitude) and verify `dotnet build` succeeds
- [x] 1.4 Add EF migration + idempotent seed (upsert by Code) for `PostalAgency` and verify `dotnet ef database update` locally seeds ~3,300 rows and re-running seed does not duplicate

## 2. Order model — agency snapshot

- [ ] 2.1 Add `OrderHeader` columns `DeliveryType` (string), `PickupAgencyCode`, `PickupAgencyName`, `PickupAgencyAddress` (all nullable for pre-change orders) and verify migration creates them
- [ ] 2.2 Update `ApplicationDbContext` / model snapshot and verify `dotnet build` succeeds

## 3. Services — geocoding and nearest-5

- [ ] 3.1 Implement `GeorefAddressService` (`HttpClient`, `GeorefOptions:BaseUrl`, 5s timeout) calling `georef/api/direcciones?direccion=&provincia=&localidad=&max=1` and verify it returns `lat/lon` for a known Mendoza address and `null` on bad address / non-2xx
- [ ] 3.2 Implement `NearestAgencyService` (haversine, province-filtered, top 5, distanceKm) and verify unit test `NearestAgencyServiceTests` passes for ranking, tie, and <5-in-province cases
- [ ] 3.3 Register services in DI (`Program.cs`) and verify checkout still resolves `CartController`

## 4. Checkout — branch search and selection

- [ ] 4.1 Add `CartController.GetNearestAgencies` (GET or POST with antiforgery) returning JSON `[{code,name,address,locality,province,distanceKm}]` using `GeorefAddressService` + `NearestAgencyService`, and verify manual call returns 5 for a Mendoza address
- [ ] 4.2 Update `Cart/Summary.cshtml` — add "Retiro en sucursal — Envío gratis" block with "Buscar sucursales cercanas" button and required radio list of 5 candidates (hidden `PickupAgencyCode` + client snapshot), and verify the picker appears and enforces selection
- [ ] 4.3 Update `CartController.SummaryPOST` to validate a branch is selected, re-resolve the snapshot from `PostalAgency` by code (never trust client name/address), persist `DeliveryType=S` + snapshot on `OrderHeader`, and verify an order created with a picked branch stores the denormalized snapshot

## 5. Admin and order history

- [ ] 5.1 Show agency snapshot on `Admin/Order/Details` (and customer order history) when `DeliveryType=S`, and verify pre-change orders render no agency block without error

## 6. Localization and copy

- [ ] 6.1 Add `es-AR`/`en-US` resx entries for the branch block, free-shipping copy, search button, validation messages, and agency fields, and verify both cultures render correctly

## 7. Tests and verification

- [ ] 7.1 Add `VaultShop.Tests` coverage: `GeorefAddressService` parsing (mock `HttpMessageHandler`) + `NearestAgencyService` haversine ordering + `SummaryPOST` rejects missing agency, and verify `dotnet test` passes
- [ ] 7.2 Run full verification `dotnet build` and `dotnet test` and verify green
