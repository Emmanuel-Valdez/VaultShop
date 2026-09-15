## Architecture & Design Decisions

- **D1: Unified Pricing Resolution.** Central helper `PricingHelper.GetPriceForUser(product, ClaimsPrincipal, previewMode)` returns `FinalWholesalePrice` when `IsInRole(Role_Company)` OR (`IsInRole(Role_Admin/Employee)` AND `previewMode == wholesale`), otherwise `FinalRetailPrice`. All storefront listings (Home/Index featured + products, Search, Favorites) and Details share this helper; cart/checkout call the same rule via `CheckoutService` overload that accepts preview flag from `HttpContext.Session`.

- **D2: Preview State Storage.** Session key `AdminPreviewMode` with values `retail|wholesale` (default `retail`). `HomeController.SetPreviewMode(mode, returnUrl)` validates mode, checks `User.IsInRole(Admin/Employee)`, writes session, and redirects to `returnUrl` (validated local URL). Non-admin POST is ignored with 403/redirect. Session already enabled in `Program.cs`; no new middleware.

- **D3: View Integration.**
  - `_Layout.cshtml`: header toggle (two-state button group or select) rendered only for `Admin/Employee`; posts to `SetPreviewMode`; preserves current URL via hidden `returnUrl`.
  - `Home/Index.cshtml`, `Home/Search.cshtml`, `Favorite/Index.cshtml`: replace direct `FinalRetailPrice` rendering with helper/partial that emits single resolved price.
  - `Home/Details.cshtml`: keep strikethrough retail + wholesale block but gate it on unified `useWholesalePrice` (company OR admin-preview), not only role check.

- **D4: Cart/Checkout Wiring.** `CartController` (`Index`, `Summary`) resolves `useWholesalePrice = User.IsInRole(Company) || IsWholesalePreview(HttpContext)` and passes it to `CheckoutService.BuildSummary` / `CreateOrder`. Same flag used by `Cart/Summary.cshtml` to render line prices. No DB schema change; wholesale eligibility remains role-based — preview only simulates display/pricing for the admin session.

- **D5: Visual Banner.** Conditional partial in `_Layout.cshtml` above `RenderBody()` when `IsWholesalePreview` is true: Bootstrap `alert-warning` with `role="status"` and `aria-label`, text `Preview: wholesale pricing`, and inline form/button to set `mode=retail`. Dismiss does not use JS-only; it posts to same endpoint. No layout shift beyond one block.

- **D6: Security and Scope.** Preview never elevates a non-admin to wholesale checkout; server re-checks role on every request. Session value is not trusted for Company users — their wholesale price comes from role regardless of session. No cookie or query-param persistence to avoid shareable wholesale links.

