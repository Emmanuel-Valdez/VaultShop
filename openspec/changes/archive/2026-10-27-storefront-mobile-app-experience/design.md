## Context

See `proposal.md` for motivation and `specs/storefront/mobile-app-experience/spec.md` for observable behavior. `Views/Shared/_Layout.cshtml` currently renders two navigation bars, two hamburger/offcanvas controls, and search forms for separate viewport regions. Customer, Admin, and Identity pages share this layout. Theme primary/accent/surface values are already deployment-configurable; the page canvas itself still uses Bootstrap's white body background. The customer Home and Search actions already define category/collection visibility and canonical filter URLs.

## Goals / Non-Goals

**Goals:**
- Keep the mobile shell limited to customer storefront routes while retaining the shared layout and existing authorized admin links.
- Make each implementation stage independently runnable and visually reviewable before proceeding.
- Reuse existing catalog filter routes and product visibility semantics.

**Non-Goals:**
- Rewriting MVC pages as a React SPA, adding a client-side router/API, or changing route/SEO semantics.
- Changing catalog content or correcting product/collection imagery and associations.
- Adding search analytics or fabricating popular-search suggestions.
- Changing the Admin or Identity experience into a mobile app shell.
- Deploying to either production store as part of implementation stages.

## Decisions

1. **Keep Razor MVC and use the existing Bootstrap/vanilla-JS stack.** Use the shared layout and Bootstrap's existing offcanvas/modal behavior for the menu and full-screen search. Opening/closing these surfaces stays client-side; choosing a route or submitting search performs the normal server-rendered GET navigation. A React migration would add a separate routing/state/API architecture without being necessary for this interaction model.

2. **Use one OpenSpec change with sequential review gates.** Keep the palette, shell, and search/catalog menu together because they share the global layout and visual system, but group the work into separately verifiable stages. The apply workflow SHALL stop at each checkpoint and wait for explicit user approval before starting the next stage. A review request or correction at a checkpoint is handled before continuing.

3. **Stage 1 changes only the light canvas and its visual verification.** Apply the portfolio cream (`#f6f3ea`) to the light-theme page canvas, keeping component surfaces and each deployment's configured navy/gold or navy/rose accents distinct. Do not alter dark-theme tokens. Review both store configurations before beginning layout changes.

4. **Stage 2 consolidates mobile navigation in the shared layout.** Below the existing desktop breakpoint, render the five agreed actions (Home, Search, Cart, My account, Menu) as one fixed bottom bar for customer storefront routes. Use CSS safe-area insets and bottom content padding. Keep existing desktop navigation at desktop widths, put informational links in the footer, and keep admin routes/role gates intact. The menu uses one Bootstrap offcanvas surface.

5. **Stage 3 adds catalog destinations and search.** Populate the menu with categories and collections that have storefront-visible, sellable products, using the same IDs and slugs as Home/Search. The global layout needs these items on pages where Home/Search view models are absent; use a small layout-level view component following the existing `ShoppingCartViewComponent` pattern, and query only the needed visible catalog data rather than materializing the full product list on every request. Search uses the existing Search GET route and query parameter semantics.

6. **Stage 4 completes the applicable audit fixes.** Correct heading semantics, readable mobile text/gutters, product-detail heading placement, and filter-row affordance. Add no decorative motion set; any interaction transition introduced along the way honors `prefers-reduced-motion`.

## Risks / Trade-offs

- [Fixed navigation can cover controls on short screens or devices with display insets] → Reserve bottom space in the page layout, apply `env(safe-area-inset-bottom)`, and verify end-of-page and checkout controls at narrow viewport heights.
- [A layout-level catalog menu can cause repeated catalog work on every page] → Keep the data query narrow and server-side, filter to currently visible/sellable catalog entries, and validate that it does not duplicate the Home/Search full product-list load.
- [Combining catalog lists and role-dependent links can expose destinations to the wrong audience] → Render only public categories/collections and preserve existing server authorization; do not weaken endpoint authorization.
- [The warm canvas may reduce component separation or contrast] → Keep warm surfaces distinct from the canvas and verify contrast in both brand configurations and both themes.
- [Review gates can leave a partially complete change between stages] → Each stage must build/run independently, and its checkpoint includes a concise list of the changed behavior and the manual review URL(s).

## Migration Plan

Implement locally in four stages: (1) cream canvas, (2) mobile shell/footer/menu, (3) catalog menu data and search overlay, and (4) remaining storefront audit corrections. At the end of each stage, run its checks, show the user the local result, and stop until the user approves or requests revisions. Production configuration/deployment is a separate decision after all stages are accepted; rollback is reverting the corresponding stage's commit/deployment artifact.

## Open Questions

- None. The five bottom-bar actions and preserving store-specific accents were confirmed with the user.
