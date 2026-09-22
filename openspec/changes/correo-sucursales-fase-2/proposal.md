## Why

Branch pickup ("retiro en sucursal") is the only free shipping method, but the buyer never sees branch hours, cannot pick a branch deterministically (geocoded nearest-5 fails on mistyped addresses and depends on Georef), emails never mention the pickup branch, and admins can still rewrite shipped orders through the app.

## What Changes

- Add `PostalAgency.Hours` (from `sucursales.json horario`, verified 3765/3765 rows) and `OrderHeader.PickupAgencyHours` snapshot (`NOT NULL DEFAULT 'no informa'`), written at order creation and admin correction, re-resolved server-side by code.
- Replace the geocoded nearest-5 flow with a deterministic cascade `Provincia → Localidad → Sucursal` in Cart Summary; **BREAKING**: remove `GetNearestAgencies`, `GeorefAddressService` pickup path, `NearestAgencyService.FindNearest` ranking path, and the search JS (keep `GetByCode` + `IsCandidate` as the single server-side resolver).
- Reuse the same cascade partial in Admin Order Details for pre-shipment branch correction; post-shipment the branch block is read-only.
- Freeze shipped orders: no edits (including cancel/refund) by anyone through the app once `OrderStatus=Shipped`; remove the admin-shipped-edit confirm flow.
- Buyer `OrderConfirmation` and `ShippingConfirmation` emails gain the branch block (name/address/hours) + 5-business-day pickup policy copy; shipping email uses in-transit-to-branch copy with the Correo tracking landing link + code.
- Order summary HTML/PDF pickup block gains the hours line.
- Harden the seed: explicit `horario→Hours` mapping, `Hours` in the field-by-field reseed compare, stale-delete guard (>10% or >200 rows aborts + logs Critical), 2 cascade indexes; `refresh_sucursales.py` becomes confirm-only (never deletes, outputs a parallel changelist) and carries `horario` through merges.

## Capabilities

### New Capabilities

- `shipping/branch-pickup`: deterministic branch cascade selection, branch hours data pipeline (JSON → seed → snapshot), snapshot semantics, pre-shipment admin correction, and the shipped-frozen edit rule.

### Modified Capabilities

- `order-lifecycle`: adds the frozen-after-shipped rule (no app edits, including cancel/refund, once shipped) and the pre-shipment admin branch-correction window.
- `billing-invoicing`: the "transactional emails stay unchanged" requirement gains an allowed delta (pickup branch block + hours + 5-day policy copy in confirmation/shipping emails); the order summary HTML/PDF pickup block gains the hours line.

## Impact

- `VaultShop.Models` (`PostalAgency`, `OrderHeader`), one EF migration + reseed; `DbInitializer` seed logic; `NearestAgencyService`/`GeorefAddressService`/`CartController` checkout path; `Summary.cshtml` + new `_BranchCascadePicker` partial; `Admin/Order/Details.cshtml` + `OrderController` (`UpdateOrderDetail`, `CancelOrder` guards); `EmailTemplates` (+ resx keys es-AR/en-US, FAQ/Terms pickup section); `OrderSummaryService`/`OrderSummaryPdfGenerator`; `tools/refresh_sucursales.py`; existing tests asserting admin-can-edit-shipped invert.
