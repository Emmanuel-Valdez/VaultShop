## Why

The theme switcher is a fixed floating button (`_ThemeToggle.cshtml`, rendered at `_Layout.cshtml:467`) in every area. On the storefront mobile shell it overlaps product content and needs a CSS offset (`site.css` `.storefront-body .bd-mode-toggle`). It is a three-option dropdown (Light / Dark / Auto), but "Auto" never shows as active because the stored or system preference always resolves to light or dark on load, so the option adds confusion without value.

## What Changes

- Replace the three-option dropdown with a two-state toggle (light / dark) that switches the theme with one click.
- Remove the "Auto" option from the UI. When the user has not chosen a theme, the initial theme still follows `prefers-color-scheme`, so existing behavior for new visitors is preserved.
- Move the toggle out of the floating button and into the navigation:
  - Desktop: in the top navbar, next to the account and cart actions.
  - Storefront mobile: inside the existing Menu offcanvas (`#sideNavbarTop`), not in the bottom bar.
  - Admin and Identity: in their existing navbar, following the same placement rule.
- Remove the floating button markup, its `bd-mode-toggle` CSS offsets, and the `prefers-color-scheme` change listener that only serves "Auto".
- Apply to all stores (VaultShop and UkiyoStudio) through the shared layout.
- **BREAKING** for users who had explicitly saved "auto" in `localStorage` under the `theme` key: treated as "no preference" and resolved from the system setting.

## Capabilities

### New Capabilities

- `ux/theme-toggle`: two-state light/dark toggle, its placement in the navigation on each area and breakpoint, and the initial theme resolution rules.

### Modified Capabilities

None.

## Impact

- `VaultShop.Web/Views/Shared/_ThemeToggle.cshtml` (replaced by a navbar-sized control, or removed in favor of an inline partial)
- `VaultShop.Web/Views/Shared/_Layout.cshtml` (remove line 467 render; add the toggle to the desktop navbar and the Menu offcanvas)
- `VaultShop.Web/wwwroot/js/utils.js` (theme state, remove "auto" branch and the listener at lines 69-74 only if no other code depends on it)
- `VaultShop.Web/wwwroot/css/site.css` (remove `.bd-mode-toggle` overrides at lines 1601-1604)
- Localization: `Light`/`Dark`/`Auto` resource keys in `Resources/Views/Shared/_ThemeToggle*.resx`; `Auto` key can be removed if unused
- Tests: HTTP test that the floating control is absent and the toggle is present in the navbar and Menu offcanvas
