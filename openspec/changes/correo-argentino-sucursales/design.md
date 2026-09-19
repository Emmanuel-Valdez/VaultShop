## Context

VaultShop checkout today collects domicile fields in `Cart/Summary.cshtml` and stores them on `OrderHeader` via `CartController.Summary/SummaryPOST` + `ICheckoutService`. There is no branch concept, no geocoding, and no `PostalAgency` table. `OrderHeader` already has the customer address and pricing is freight-baked (`AvgShippingCost` in category final prices). This change adds only retiro en sucursal (no domicilio toggle), keeps address required (to geocode the nearest branches), and defers map and live PAQ.AR feed. See `proposal.md` for motivation.

## Goals / Non-Goals

**Goals:**
- 5 nearest Correo branches surfaced at checkout, ordered by haversine distance from the customer's Georef-geocoded address.
- Georef AR as primary geocoder (free, no key, national Argentine coverage) with graceful fallback when unavailable.
- Static snapshot → `PostalAgency` seed with lat/lon; order stores denormalized agency snapshot.
- Minimal UI in `Summary` (no new checkout page, no map), localized copy "Envío gratis a sucursal".

**Non-Goals:**
- Toggle domicilio vs. sucursal (only sucursal this change).
- Map/Leaflet rendering (phase 2).
- Live PAQ.AR agencies feed / agreement-gated endpoints.
- Shipping-cost calculation, surcharge, or pricing change.
- PAQ.AR order creation / labels / tracking.

## Decisions

**Snapshot acquisition — wsFacade scrape (proven, no auth), gist as base**
- The site-internal `wsFacade.php` proved viable without credentials: `localidadesconsucursales` + per-localidad `sucursales` returns cards + `L.marker([lat,lng])` + service ids (`rel`). `tools/refresh_sucursales.py` (stdlib-only) re-runs monthly; merge by Code (CABA only exposes codes) / exact (name, locality) / normalized address; `LastVerifiedUtc` + `Source` track confirmations. The 2020 gist stays as base (only source of CPA + split street/number).
- Live PAQ.AR agencies feed still deferred until creds.

**Seeding — EF entity `PostalAgency`**
- Store snapshot in DB (`VaultShop.DataAccess`, EF migration). Columns: `Code` PK (CODIGONIS), `Name`, `Street`, `Number`, `Locality`, `City`, `Province`, `ProvinceCode`, `PostalCode`, `Latitude`, `Longitude`. Seed via `HasData` / `DbInitializer` upsert by `Code` (idempotent reseed).
- Alternative rejected: bare JSON loaded at runtime (no queryability, no admin visibility, awkward testing).

**Geocoding — `GeorefAddressService` (HttpClient, typed)**
- Calls `https://apis.datos.gob.ar/georef/api/direcciones?direccion=&provincia=&localidad=&max=1`. Parses `direcciones[0].ubicacion.{lat,lon}`. Timeout ~5s, `max` 1, returns `null` coords on failure (no throw).
- Caller (`NearestAgencyService` / controller) treats `null` coords as "no distance ordering" and falls back to province-filtered list. Logs at Information/Debug without PII.

**Nearest-5 — `NearestAgencyService` (pure haversine, province-filtered, verified + parcel-capable)**
- Candidates are drawn ONLY from verified rows (`Source="correo"`) that offer parcel handover (**service `40` in `Services`**; `40` alone, not `29`). Unverified gist-only rows and branches without `40` (ej. OBELISCO, TIENDA FILATELIA) SHALL NOT be shown at checkout. If a shopper asks for a missing branch, its parcel capability is confirmed manually and it is added/updated via the monthly refresh.
- Source of truth is the Correo site scrape, not the 2020 gist: same address = same branch, site name wins; obvious site typos live in `NAME_OVERRIDES` in the script.
- **UP (Unidad Postal; map pins labeled AGENCIA) are included as candidates** — 95% offer service `40` in CABA, but only classic parcel/postal services (no monetarios, telegramas, pagos, SUBE). Ingested with stable synthetic codes (`UP-{provinceCode}-{hash6}` over name|locality|street, `Kind="UP"`), `Services` from the card `rel`. They have no CODIGONIS by nature. Risk: PAQ.AR may not route parcels to codeless points — revisit if label creation rejects them.
- If coords available: filter by `Province`/`ProvinceCode` derived from `OrderHeader.State`, compute haversine (km), sort, take 5. If province has <5, return all. If province empty (mismatch), broaden to national top-5.
- If coords absent: filter by province+locality, return up to 5 alphabetically (no distance shown).
- Service is unit-testable (distance ranking); controller only orchestrates. No caching needed (dataset ~3,300 rows, in-memory scan trivial).

**Checkout wiring — `CartController` + `Summary.cshtml` AJAX**
- New endpoints: `GET /Customer/Cart/GetNearestAgencies?street=&city=&state=&postalCode=` (or POST with AntiForgery) returning JSON `[{code,name,address,locality,province,distanceKm}]`. Address fields are the current form values (not yet persisted).
- `Summary.cshtml`: address form unchanged + branch block under it: "Retiro en sucursal — Envío gratis" heading, "Buscar sucursales cercanas" button, radio list of 5 (required). Selected code stored in hidden `OrderHeader.PickupAgencyCode` (+ name/address snapshots read from the chosen candidate). `SummaryPOST` validates: branch required; resolves full snapshot from `PostalAgency` by code (never trusts client-supplied name/address).
- Validation: server re-resolves branch by code; invalid code → ModelState error.

**Order persistence — `OrderHeader` snapshot columns**
- Add `DeliveryType` (string, "S"), `PickupAgencyCode`, `PickupAgencyName`, `PickupAgencyAddress` (single denormalized string: "CALLE NUM, LOCALIDAD, PROVINCIA CPA"). Nullable for pre-change orders. No FK to `PostalAgency`. Migration.

**Admin visibility — `Admin/Order/Details`**
- Show agency block when `DeliveryType=S`. No new admin CRUD for agencies (read-only snapshot; reseed covers updates).

**i18n — `es-AR`/`en-US` resx**
- All new labels (branch block heading, free-shipping copy, search button, validation messages, agency fields) localized following existing `Resources/` pattern.

## Risks / Trade-offs

- **Georef accuracy varies by region** → some addresses geocode imprecisely; ranking may be slightly off — Mitigation: province filter limits error radius; fallback province list still lets the shopper pick.
- **Snapshot staleness (3,300 branches, hours/address changes)** → seed can lag — Mitigation: reseed is idempotent; later PAQ.AR feed can replace the source without schema change.
- **wsFacade is undocumented/internal** → may change or require headers/cookies — Mitigation: spike early, fallback path (gist+Georef) keeps MVP unblocked.
- **No live availability (pickup_availability flag)** in static snapshot — Mitigation: snapshot includes every branch; filtering by service flag deferred until live feed.

## Migration Plan

1. Add `PostalAgency` entity + `OrderHeader` snapshot columns → EF migration.
2. Seed snapshot (`sucursales.json` checked into repo under `data/` or embedded resource) on migration/DbInitializer.
3. Deploy; cart/checkout change is additive — pre-change orders display no agency block (nullable columns).
4. Rollback: revert migration; orders created with snapshot keep their denormalized address but lose the dedicated columns (data would need manual restore).

## Open Questions

- None blocking. Copywording for "Envío gratis a sucursal" and the exact agency block layout in Admin will be settled in implementation review.
