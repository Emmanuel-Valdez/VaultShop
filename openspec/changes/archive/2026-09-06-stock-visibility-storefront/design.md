# Design: stock-visibility-storefront

## Context

Product cards on `Home/Index` (two loops) and `Home/Search` render image, category, name, price — no stock signal. The `Index`/`Search` actions return full `Product` entities via `GetAll(..., includeProperties: "Category,ProductImages")` with no projection, so `StockQuantity` already reaches every view; no controller or query change is needed. `Details.cshtml` already has an out-of-stock badge + disabled Add-to-Cart (`StockQuantity == 0`) and a stock-capped stepper (`data-stock` + inline JS setting `input.max`), but never shows the quantity, and its JSON-LD block hardcodes `"availability": "https://schema.org/InStock"`. The `.product-card__image-link` element is already `display:block; overflow:hidden; aspect-ratio:5/6`, making it a ready positioning context for an overlay strip. CSS is centralized in `wwwroot/css/site.css` with `[data-bs-theme=dark]` overrides; Bootstrap Icons (`bi-*`) are already loaded.

## Goals / Non-Goals

**Goals:**
- One reusable CSS class + one conditional markup block per card loop, zero JS.
- Card banner conveys out-of-stock with localized text + icon (never color alone).
- Detail page quantity line reuses the existing `@string.Format(Localizer[...].Value, ...)` localization pattern.

**Non-Goals:**
- Low-stock banners or thresholds, exact quantities on cards, cart-page indicators (noise — items are already in the cart).
- Extracting a shared product-card partial (the three loops have diverged markup already; consolidation is separate tech debt).

## Decisions

### D1: Absolute-positioned strip inside the image link, not a full-image overlay
The banner is a bottom strip (`position:absolute; inset:auto 0 0 0`) inside `.product-card__image-link` with a translucent `rgba(..., 0.65–0.72)` background, rather than a full-image darkening overlay or a card-footer row.
- *Why:* a strip keeps the product image and price fully visible (translucency requirement), survives any image aspect, and anchors to an existing positioned container — no markup restructuring.
- *Alternative rejected:* full-image overlay with centered text — hides the product, heavier CSS, and reads as an error state rather than a status.

### D2: Banner renders only when `StockQuantity == 0`; no low-stock state
Cards show a binary in-stock/out-of-stock signal only.
- *Why:* stock fluctuates per purchase; a "low stock" banner needs a threshold config for a value that never changes and trains users to ignore badges (banner fatigue). Out-of-stock is the only user-blocking, stable fact worth card real estate.
- *Alternative rejected:* `else if (StockQuantity <= N)` low-stock banner — deferred; if urgency marketing becomes a roadmap item it is a one-line addition on the same block.

### D3: Quantity line on Details renders only when stock > 0, placed after the price block
`StockAvailable = "{0} available" / "{0} disponibles"` via `@string.Format(Localizer[...].Value, Model.Product.StockQuantity)`.
- *Why:* "0 available" would duplicate the existing badge + disabled button; the price block is where purchase intent is highest, and it explains why the stepper's plus button stops.
- *Alternative rejected:* showing the quantity inside the badge area — mixes availability status with stock accounting and breaks the badge's simple binary semantics.

### D4: No new JS, no new dependencies, no controller changes
Everything is server-rendered Razor conditionals over data already in the model.
- *Why:* smallest diff; the stepper already caps via `data-stock` + `input.max`.

## Risks / Trade-offs

- **Dark-theme contrast** → the dark override uses near-black translucent (`rgba(0,0,0,0.65)`) under white text; verify contrast ratio against the actual `--theme-primary-dark-rgb` value in light theme during implementation.
- **Search card links lack `aria-label`** (pre-existing gap; Index cards have it) → the banner text rides inside the link content for screen readers anyway; fixing the Search `aria-label` is included as an opportunistic one-liner task.
- **Banner inside the link adds no focus target** → inert content, no keyboard impact; `aria-hidden` on the icon, text exposed.
- **Stale stock on cached pages** → cards render per-request from the DB like everything else on the page; no new caching introduced.
