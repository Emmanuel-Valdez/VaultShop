## Context

See proposal.md for motivation. Current state that shapes the approach:

- `PricingCalculatorService.GetFinalPrices()` computes retail as `(C/(1−g) + S)/(1−t)` where `C` is the cost basis, `g` the profit margin, `S` the category `AvgShippingCost`, and `t` the aggregate configured percentage costs. Shipping passes through percentage costs but not the profit margin. Wholesale already excludes shipping.
- Checkout (`CartController.Summary/SummaryPOST` + `CheckoutService`) always requires a Correo branch (`DeliveryType="S"`, snapshot by code) and shows hardcoded "Envío gratis a sucursal" copy. Domicile province/locality are free-text inputs prefilled from `ApplicationUser`.
- `OrderTotal` is the amount charged by Stripe/Mercado Pago sessions (MP sync validates `TransactionAmount == OrderTotal`); the payment-method discount prorates across product lines only.
- Branch/postal-unit candidates come from `PostalAgency` via `IBranchLookupService` (province → locality → branch); selection is manual, and the archived branch-pickup spec explicitly forbids a geocoded nearest search.
- The 26 Gran Mendoza localities all exist in the branch catalog; catalog spellings vary in case/accents (`LAS CANAS` for `Las Cañas`, `KILOMETRO 11`).
- No living spec owns shipping behavior (the two archived Correo changes were never synced into `openspec/specs/`), so this change creates the shipping capability and documents the charge rules.

## Goals / Non-Goals

**Goals:**

- Per-product free/paid shipping split that is revenue-neutral: removing `S` from an unflagged product's suggested retail lowers it by exactly `S/(1−t)`, the same amount the checkout charge adds back.
- One frozen shipping charge per order, computed once server-side, included in the charged total and rendered everywhere the order total is shown.
- Gran Mendoza modality flow (motorcycle, vendor-arranged pickup, Correo pickup) driven by the locality selection, with human verification for the two judgment calls (Maipú center, vendor-pickup abuse).

**Non-Goals:**

- Correo rate API or live tariff quotes (does not exist); nearest-branch search, geocoding, or distance ranking.
- A machine-checkable Maipú "center" boundary (no reliable data); it stays a human confirmation.
- Any change to wholesale (Company) shipping handling, payment-method discounts, coupon/promotion logic, or the archived branch-correction flow.
- Distinct handling or labeling of postal units (`UP`) versus full branches; both are valid pickup points.

## Decisions

1. **Flag on `Product`, migration backfills existing rows as free.**
   Add `Product.FreeShipping` (bool, model default `false`). The EF migration adds the column and runs an explicit `UPDATE` setting it to `true` for every row that exists at migration time; rows inserted later take the model default. Alternative considered: backfill through application code (rejected: a second deploy step, migration-order risk). The admin upsert gains a checkbox; the price calculator and help texts follow.

2. **The calculator keeps its composition; the flag only gates the `S` term.**
   `retailWithShipping = retailWithProfit + (product.FreeShipping ? S : 0)` keeps every existing invariant (margin not applied to shipping, percentage costs applied). No price churn at deploy: all existing products are flagged free, so every suggested price is unchanged. Publishing prices for a new unflagged product writes the lower (shipping-free) suggestion.

3. **Charge computation lives in `CheckoutService` as the single source.**
   `ShippingCharge = anyLineFree ? 0 : Round(max(S of non-free lines) / (1 − t/100), 2)`. `t` comes from `GetTotalPercentageCost()` (the same aggregate the calculator uses). The cart, summary, `SummaryTotals` and `CreateOrder` all reuse it, so display and persisted total cannot diverge. Cart queries must include `Product.Category` (today `BuildSummary` does not). The evaluator is untouched: the charge is added after discount evaluation, so coupon/promotion/payment discounts and minimum-subtotal rules keep operating on the product subtotal only.
   - Cart display amount when the cart qualifies: the same formula over all lines (what shipping would cost), shown struck through next to the free label.
   - Alternative considered: percentage per payment gateway (rejected: one standardized amount was agreed; the method is unknown at cart time and config can change).

4. **Delivery modality encoded by extending `DeliveryType`.**
   Keep `"S"` = Correo branch/postal-unit pickup (existing orders, admin correction gates, confirmation email block all keep working) and add `"M"` = motorcycle, `"V"` = vendor-arranged pickup. No new column. Motorcycle and vendor-pickup orders store no branch snapshot; the delivery locality rides on the existing domicile `City`/`State` fields (now catalog-sourced). Alternative considered: a new enum column (rejected: churn for admins and existing `=S` gates, no added value).

5. **Order gains `ShippingCharge` (decimal, frozen) and it is part of `OrderTotal`.**
   `CreateOrder` stamps the computed charge before the `OrderTotal <= 0` check and before persistence. `BuildPaymentSessionLineItems` appends one shipping line item (quantity 1) that is excluded from the payment-discount proration, keeping the MP total equality check and the "charged amount equals frozen total" requirement intact.

6. **Domicile selection reuses the branch catalog; the branch cascade collapses into one selection.**
   `Summary` replaces the free-text province/locality inputs with selects fed by `IBranchLookupService.GetCandidateProvinces/GetCandidateLocalities`, and the branch picker loses its own province/locality step: the shopper picks province → locality once, then the page offers modality radios (motorcycle and vendor pickup first when the cart is paid and the locality is Gran Mendoza, then Correo pickup with that locality's branch radio list via the existing `BranchBranches` endpoint). Prefill from `ApplicationUser` remains best-effort (select when the stored text matches a catalog locality). Server-side: reject province/locality pairs absent from the catalog; require a branch only when the modality is Correo. `DeliveryType="S"` for Company checkout stays exactly as today (branch required, no charge).
   - Alternatives considered: a second independent cascade for the domicile (rejected: two competing locality pickers confuse), free-text with fuzzy matching (rejected: not verifiable).

7. **Gran Mendoza list as code constants with normalized matching.**
   `SD` gains the canonical 26-locality list plus a matcher that normalizes case and accents (strip diacritics, uppercase) so `LAS CANAS` matches `Las Cañas`. Zone membership only toggles modality availability — the charge itself is zone-independent — so a forged locality cannot game pricing; the residual abuse vector (vendor pickup) is covered by decision 9. Alternative considered: zone table in the database (rejected: constant list is small, versioned, and unit-testable; revisit if zones multiply).

8. **Dynamic total on modality change via the existing `SummaryTotals` pattern.**
   Extend `SummaryTotals` to accept the modality and return the updated total plus shipping display values; modality radios update the summary like the payment-method radios already do. Selecting vendor pickup drops the charge; Correo/motorcycle keep it (or $0 on free carts).

9. **Maipú and vendor-pickup abuse stay human-verified, recorded in the order.**
   No new automated gate or admin note field. The order records the chosen modality (and the receipt wording for vendor pickup: delivery to be arranged at the Microcentro of Mendoza); store staff confirm Maipú motorcycle addresses and vendor-pickup usage via WhatsApp before dispatch, and any later domicile shipment for a vendor-pickup order is arranged and charged outside the store. This matches the agreed policy and keeps the system honest to what the shopper selected.

10. **Copy and badge stay minimal; resx in both cultures.**
    Badge: "Envío gratis" / "Free shipping" on listings and details (retail viewers only). Cart: free label with struck-through standard amount, or the charge as an item-ish line; no extra explanatory notes. Modality labels: Correo pickup, motorcycle, vendor-arranged pickup. FAQ (es/en) gets the free/paid rule and modality summary; admin help mentions the flag's effect on suggested prices.

## Risks / Trade-offs

- [Gross-up uses the aggregate percentage-cost configuration, which averages gateway fees across methods] → The amount is frozen at order creation; configuration changes only affect new orders. Accepted approximation, same as the price calculator today.
- [Catalog-restricted domicile localities: a home town without a pickup point cannot be selected] → The catalog covers every locality that has a Correo point; the shopper picks the nearest listed locality. If a gap appears, it is fixed by the next branch-catalog refresh, not by allowing free text.
- [Mixed carts (free + paid) ship free while the paid lines' shipping is not priced into anything] → Intended business rule to reduce friction; the free product's list price covers its category share.
- [Vendor pickup could be used to obtain unearned free handling] → Receipt wording + human verification before dispatch; later shipments charged outside. No automated detection by design.
- [Maipú "center" has no machine boundary; motorcycle could be offered for an out-of-area address] → Human confirmation before dispatch; the store re-arranges (Correo or vendor pickup) with the customer.
- [New `DeliveryType` values leak into surfaces that assumed only `S` or null] → Confirmation/history/admin/PDF blocks gate on `=S` for the branch snapshot; new values render their modality line and no branch; verified in tasks.
- [Badge promises nothing the cart would not grant] → Badge renders only for flagged products to retail viewers; the cart free rule is driven by the same flag.

## Migration Plan

1. EF migration: add `Product.FreeShipping` (default `false`) + backfill `true` for existing rows; add `OrderHeader.ShippingCharge` (default 0). Deploy with the feature.
2. Pre-change orders render as before (null/`"S"` modality, zero charge).
3. Rollback: revert deployment and migration; orders created meanwhile keep `"M"`/`"V"` values that old code treats as unknown — they are read-only strings, so no data repair is strictly required, but modal-rail views revert to plain branch blocks.

## Open Questions

- Exact wording of the new es-AR/en-US labels (modality names, FAQ answers) is finalized in implementation review, as with previous copy-only deltas.
- Whether Maipú eventually needs a codified center boundary (postal codes or street list) remains open; the human-confirmation route is the interim answer and no spec change is needed to adopt a boundary later.
