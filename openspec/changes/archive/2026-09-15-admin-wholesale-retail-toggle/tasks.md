## 1. Preview State & Helper
- [x] 1.1 Add session helper/extension `IsWholesalePreview(HttpContext)` and constant for `AdminPreviewMode` key; guard to Admin/Employee roles only.
- [x] 1.2 Add `HomeController.SetPreviewMode(mode, returnUrl)` POST/GET that validates `retail|wholesale`, checks role, writes session, validates local `returnUrl`, and redirects.

## 2. Storefront Pricing Integration
- [x] 2.1 Implement `PricingHelper` (or view helper/partial) that resolves single price: wholesale when `Role_Company` OR `(Admin/Employee + wholesale preview)` else retail.
- [x] 2.2 Replace hardcoded `FinalRetailPrice` in `Home/Index.cshtml` (products + featured), `Home/Search.cshtml`, `Favorite/Index.cshtml` with unified helper; update `Home/Details.cshtml` gate to use unified `useWholesalePrice` for strikethrough block.
- [x] 2.3 Update `CartController` (`Index`, `Summary`) and `CheckoutService` (`BuildSummary`/`CreateOrder` call sites) to compute `useWholesalePrice` from role OR preview and render cart/summary lines with the resolved price.

## 3. UI & Banner
- [x] 3.1 Add header toggle in `Views/Shared/_Layout.cshtml` visible only to `Admin`/`Employee`; posts to `SetPreviewMode` preserving current URL.
- [x] 3.2 Add wholesale-preview indicator banner partial in `_Layout.cshtml` (above `RenderBody`) with `role="status"`, descriptive text, and exit-to-retail control; verify keyboard operability.

## 4. Testing
- [x] 4.1 Add tests: Company sees wholesale on listings/cart, retail customer sees retail, admin in wholesale preview sees wholesale, admin in retail preview sees retail, non-admin cannot set preview, banner visibility.
- [x] 4.2 Run `dotnet build` and `dotnet test` and verify green build.

