# Proposal: Correo Argentino sucursales pickup

## Why

Most VaultShop customers live outside Mendoza. Offering a free-shipping pickup at a nearby Correo Argentino branch converts better than a domicile delivery with a visible freight charge. Today VaultShop has no sucursal data or branch-selection step, so the store cannot ship to branches at all.

## What Changes

- Add a static snapshot of Correo Argentino branches (national, with lat/lon) seeded into the database via `PostalAgency`.
- At checkout (`Cart/Summary`), keep the customer's address form (required) to geocode the domicile and surface the 5 nearest branches (`Georef AR` → haversine, province-filtered). The shopper must pick one; the order is `DeliveryType=S`.
- Persist the chosen branch as a snapshot on `OrderHeader` (`PickupAgencyCode/Name/Address`) and surface it in Admin order details and order history.
- Show "Envío gratis a sucursal" copy at the pickup selector. Pricing remains baked-in (no new freight calculation).
- Snapshot acquisition: spike probes `wsFacade.php` / site-internal sucursales endpoint for coordinates; fallback is the national gist plus one-time Georef batch geocoding to build the JSON with coords.

## Capabilities

### New Capabilities
- `shipping/correo-pickup`: branch pickup at checkout — address geocoding (Georef AR), 5 nearest agencies, order agency snapshot, admin visibility, and "Envío gratis a sucursal" messaging.

### Modified Capabilities
- None

## Impact

- Data: new entity `PostalAgency` + `OrderHeader` columns + migration/seed.
- Services: `GeorefAddressService` (external `apis.datos.gob.ar`), nearest-5 haversine service.
- Web: `VaultShop.Web/Areas/Customer/Controllers/CartController.cs` (`Summary`/`SummaryPOST` + AJAX `GetNearestAgencies`), `Summary.cshtml` branch selector, `Admin/Order` details views, `_ValidationScriptsPartial` for required agency selection, resx (`es-AR`/`en-US`).
- Tests: haversine ranking + Georef parsing + checkout validation.
- No pricing change, no toggle domicilio/sucursal, no map, no PAQ.AR order creation in this change.
