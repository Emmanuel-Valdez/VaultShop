## Context

Current implementation:
- `VaultShop.Web/Views/Shared/_ThemeToggle.cshtml` is a Bootstrap dropdown with three items (`data-bs-theme-value` light / dark / auto), rendered as a `position-fixed bottom-0 end-0` button by `_Layout.cshtml:467`.
- `VaultShop.Web/wwwroot/js/utils.js` stores the choice in `localStorage['theme']`, sets `data-bs-theme` on `<html>`, and listens to `prefers-color-scheme` changes (lines 69-74). `getPreferredTheme()` never returns `auto`, so the "Auto" item never shows as active on load.
- `site.css:1596-1605` offsets the floating button above the storefront mobile bottom bar (`.storefront-body .bd-mode-toggle`).
- Bootstrap 5.3 dark mode is driven entirely by `data-bs-theme` on `<html>`; no per-component theme code is required.

## Goals / Non-Goals

**Goals:**
- One click switches light/dark.
- Toggle lives in navigation on every area; no floating element.
- Initial theme keeps following the system preference for visitors with no stored choice.

**Non-Goals:**
- Changing palette, tokens (`--theme-*`), or any dark-mode CSS rules.
- Changing the storage key (`theme`) or its values beyond treating `auto` as unset.
- Adding dependencies or a theme framework.

## Decisions

1. **Reuse the existing `setTheme`/`getStoredTheme`/`setStoredTheme` functions in `utils.js`.** Replace the dropdown click handlers with one toggle handler that computes the next theme from the current `data-bs-theme` and calls the same setters. This keeps the storage behavior unchanged.

2. **Treat stored `auto` as unset.** In `getPreferredTheme()`, accept only `light` or `dark` from storage; anything else falls through to `prefers-color-scheme`. Removes the "Auto" UI without a migration step.

3. **Keep the `prefers-color-scheme` listener only as far as the initial theme needs it.** Once the user has a stored choice, the listener does nothing. Since "Auto" is gone, the listener is only useful for visitors with no stored value. Keep it (3 lines) rather than adding state to detect it; it is the smallest correct behavior.

4. **Partial shape.** Replace `_ThemeToggle.cshtml` with a small partial that renders one `<button type="button" id="bd-theme">` with a sun and a moon icon (show one via CSS by `data-bs-theme`), and an accessible label. Render it in two places through the partial: the desktop navbar (`d-none d-lg-*` as the navbar requires) and the Menu offcanvas (`d-lg-none`). Both placements use the same markup, so there is one piece of JS.

5. **Icons from the existing SVG sprite.** The file already defines `sun-fill` and `moon-stars-fill`; reuse them. No new icon package.

6. **Localization.** Keep `Light`/`Dark` resource keys for the accessible label. Remove the `Auto` key only after a grep confirms no other usage.

## Risks / Trade-offs

- [Visitors who chose "auto" lose explicit auto] → Intended; they fall back to system preference, which is what "auto" did on load anyway.
- [Desktop navbar space] → The navbar already holds account and cart; a 36px icon button fits. Verify at 992px width.
- [Two DOM copies of the control share the same id] → Use a class selector (`[data-theme-toggle]`) in JS instead of `#bd-theme`, and use `d-none` / `d-lg-*` so only one is visible per breakpoint.
- [Menu offcanvas is only on the storefront] → Admin and Identity navbars get the desktop placement; mobile admin/identity placement follows the existing navbar pattern and is verified in tasks.

## Migration Plan

One pass: update `utils.js`, replace the partial, move the render point, remove the floating CSS, update localization, add HTTP tests, then build and manual check at 390px and 1280px in both stores. Rollback is reverting the commit.

## Open Questions

None.
