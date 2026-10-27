## 1. Stage 1 — Warm light canvas

- [x] 1.1 Apply the shared `#f6f3ea` canvas only to the public storefront light theme, retaining deployment-specific primary/accent colors, existing surfaces, and the dark theme; verify the CSS/theme values in both store configurations.
- [x] 1.2 Build and inspect VaultShop and UkiyoStudio at mobile and desktop widths in light and dark themes; verify the cream does not reduce text, control, or surface contrast.
- [x] 1.3 Stage 1 review gate: present the local visual result and changed files to the user, then stop. Do not start Stage 2 until the user approves or requests revisions.

## 2. Stage 2 — Mobile app shell and catalog menu

- [x] 2.1 Add the customer-storefront bottom navigation with Home, Search, Cart, My account, and Menu actions below the existing desktop breakpoint; hide it on Admin and Identity routes and retain the desktop navigation.
- [x] 2.2 Remove the duplicate mobile hamburger controls, move informational navigation links into the footer, and preserve current role-based account/admin visibility; verify desktop navigation and authorized links remain available.
- [x] 2.3 Populate the single mobile menu with storefront-visible categories and collections using the existing filter IDs/slugs; load only the required visible/sellable catalog data and verify it does not materialize the full Home/Search product list on every request.
- [x] 2.4 Verify fixed-bar safe-area and page-bottom spacing, menu focus/keyboard behavior, catalog destinations, and checkout controls at narrow viewport sizes; run `dotnet build VaultShop.sln` and relevant tests.
- [x] 2.5 Stage 2 review gate: present screenshots/local URLs and behavior summary, then stop. Do not start Stage 3 until the user approves or requests revisions.

## 3. Stage 3 — Full-screen search

- [x] 3.1 Add the mobile full-screen search overlay with a labeled query field, close action, Escape handling, visible focus, and focus restoration; verify opening/closing leaves the current URL unchanged.
- [x] 3.2 Submit queries through the existing Search GET route and `searchString` parameter; verify category, collection, and text-filter route semantics remain unchanged.
- [x] 3.3 Verify keyboard operation, narrow-screen layout, and reduced-motion behavior; run `dotnet build VaultShop.sln` and relevant tests.
- [ ] 3.4 Stage 3 review gate: present screenshots/local URLs and search behavior summary, then stop. Do not start Stage 4 until the user approves or requests revisions.

## 4. Stage 4 — Storefront audit refinements

- [ ] 4.1 Add meaningful page-level `h1` headings to Home and Login and correct the audited mobile text sizes and consistent page gutters; verify heading structure and both supported locales.
- [ ] 4.2 Adjust mobile/tablet product-detail layout so its heading appears early, and improve filter-row overflow affordance without breaking existing filter navigation; verify at 390px, 768px, and desktop widths.
- [ ] 4.3 Run `dotnet build VaultShop.sln` and `dotnet test VaultShop.sln`; manually verify Home, Search, product detail, Login, Cart, and checkout in both brand configurations, both themes, keyboard navigation, and reduced-motion mode.
- [ ] 4.4 Stage 4 review gate: present the completed local experience and verification results; wait for user acceptance before treating the change as complete or discussing production rollout.
