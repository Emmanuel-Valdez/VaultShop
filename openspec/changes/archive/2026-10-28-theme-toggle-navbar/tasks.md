## 1. Theme logic

- [x] 1.1 In `VaultShop.Web/wwwroot/js/utils.js`, make `getPreferredTheme()` accept only stored `light` or `dark`; any other stored value falls back to `prefers-color-scheme`.
- [x] 1.2 Replace the three-item click handlers with one toggle handler on `[data-theme-toggle]` that flips the current `data-bs-theme` and calls `setStoredTheme` and `setTheme`. Remove `showActiveTheme` if it has no remaining callers.

## 2. Control placement

- [x] 2.1 Replace `VaultShop.Web/Views/Shared/_ThemeToggle.cshtml` with a single-button partial (sun/moon from the existing sprite, accessible label).
- [x] 2.2 Remove the floating render at `_Layout.cshtml:467`.
- [x] 2.3 Render the partial in the desktop top navbar (visible at `lg` and up).
- [x] 2.4 Render the partial inside the Menu offcanvas `#sideNavbarTop` for storefront mobile (visible below `lg`). Confirm the bottom bar does not contain it.
- [x] 2.5 Confirm Admin and Identity navbars also render the control at desktop and mobile widths.

## 3. Styling

- [x] 3.1 Remove `.storefront-body .bd-mode-toggle` overrides in `wwwroot/css/site.css` (lines ~1601-1604) and any `.bd-mode-toggle` rules that remain unused.
- [x] 3.2 Verify no `bd-mode-toggle` references remain (grep).

## 4. Localization

- [x] 4.1 Keep `Light` and `Dark` keys for the accessible label; grep for `Auto` and remove its resource entries only if unused.

## 5. Tests

- [x] 5.1 Add an HTTP test: a storefront page does not contain the floating `bd-mode-toggle` markup, and contains exactly one theme toggle in the desktop navbar and one in the Menu offcanvas.
- [x] 5.2 Add an HTTP test: the theme toggle is present on an Identity page and an Admin page.

## 6. Verification

- [x] 6.1 `dotnet build VaultShop.sln` with 0 warnings and `dotnet test VaultShop.sln` green.
- [x] 6.2 Manual check at 390px and 1280px on Home, Product detail, Cart, Login, Manage account, and an Admin page, in both stores: toggle switches theme, choice persists after reload, and no content is covered.
- [x] 6.3 Manual check: clear `localStorage.theme` with the OS in dark mode, reload, and confirm dark is applied.
