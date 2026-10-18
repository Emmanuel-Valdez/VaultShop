## Why

Checkout today promises "free shipping to a Correo branch" on every order via hardcoded copy, because `Category.AvgShippingCost` is baked into every retail list price. Correo Argentino offers no usable rate API, so the store cannot quote exact charges — it needs a product-level free/paid shipping rule built on the existing `Category.AvgShippingCost`, plus real Gran Mendoza delivery modalities (motorcycle, vendor-arranged pickup), to reduce purchase friction without silently eating shipping cost on every order.

## What Changes

- Add a per-product free-shipping flag. Existing products are backfilled as free shipping (their current prices already include it); new products default to no free shipping.
- Suggested retail price SHALL include `Category.AvgShippingCost` only for free-shipping products. Non-free products get a lower suggested price.
- Add a cart/checkout shipping charge for orders whose lines are all non-free: charged once per order, equal to the largest `Category.AvgShippingCost` among paid lines, grossed up by the configured percentage costs, never discounted, and $0 when the cart contains at least one free-shipping line.
- Cart shows the shipping as a line: "Envío gratis" with the standard amount struck through when the cart qualifies; otherwise the charge added to the total like one more item.
- **BREAKING** (checkout flow): replace the free-text domicile city/province inputs and the single branch block with: province → locality selection (from the Correo branch catalog, no free text), then modality options. Gran Mendoza localities (the fixed 26-locality list) offer motorcycle delivery and vendor-arranged pickup in Microcentro first, followed by Correo branch/postal-unit pickup; every other locality/province offers Correo pickup only. Branch selection stays manual (no proximity search).
- Free-shipping carts in Gran Mendoza keep Correo and motorcycle at $0 and hide vendor-arranged pickup. Motorcycle covers all 26 Gran Mendoza localities; Maipú motorcycle deliveries are confirmed by a human before dispatch (no automatic "centro" boundary).
- Vendor-arranged pickup (`acordar con el vendedor`) is free and recorded on the order as such; if a shipment to domicile is later requested, it is arranged and charged outside the store.
- Persist the frozen shipping charge and delivery modality on the order; render them on cart, checkout, confirmation, customer history, admin detail, HTML/PDF summary and order emails.
- Retail-only "Envío gratis" badge on storefront listings/details (with the shipping amount struck through on the cart display). Wholesale (Company) orders: no badge, shipping stays coordinated outside the system as today.

## Capabilities

### New Capabilities

- `shipping/methods-and-charges`: product free-shipping flag semantics, cart and checkout shipping charge computation, Gran Mendoza locality list and modality availability (Correo branch/postal unit, motorcycle, vendor-arranged pickup), domicile province/locality selection, frozen order shipping charge and modality, retail badge, wholesale exclusion.

### Modified Capabilities

- `pricing`: suggested retail price includes the category average shipping cost only for free-shipping products (calculator and published prices).
- `billing-invoicing`: order summary renders the persisted shipping charge and delivery method; order emails may carry the frozen shipping line (third allowed delta alongside email timing and WhatsApp copy).

## Impact

- `VaultShop.Models`: `Product` free-shipping flag; `OrderHeader` frozen shipping charge and delivery modality; `OrderSummaryViewModel` shipping fields.
- `VaultShop.DataAccess`: EF migration (new columns; backfill existing products as free shipping).
- `VaultShop.Web`: `PricingCalculatorService` (shipping inside list price only when flagged), `CheckoutService` (charge computation, freeze on order, payment-session shipping line item), `CartController` + `Cart/Summary` view and resx (locality cascade, modality radios, charge line), admin product upsert + price calculator table + help, `OrderSummaryService`/`OrderSummaryPdfGenerator`, `EmailTemplates`, `SD` constants (Gran Mendoza localities, delivery types).
- Payments: `OrderTotal` includes the charge; Stripe/Mercado Pago session line items add a non-discounted shipping line that sums exactly to the frozen total (MP amount validation unchanged).
- Tests: pricing calculator, checkout charge rules, modality rendering, order persistence and email/PDF surfaces.
