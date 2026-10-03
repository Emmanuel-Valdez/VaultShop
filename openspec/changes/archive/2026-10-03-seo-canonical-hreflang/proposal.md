## Why

Every `hreflang` alternate the site emits points at a URL that does not exist: `_Layout.cshtml` builds them as `SiteUrl + "/es-AR" + Context.Request.Path`, but `Request.Path` already contains the culture segment, so the page advertises `/es-AR/es-AR/Customer/Home/Index`. Search engines discard a cluster when one alternate is broken, so the site loses its strongest "same page, other language" signal on **100% of pages**. The same `<head>` also hardcodes `<html lang="en">`, and `SetLanguage` swaps cultures with `string.Replace`, which corrupts query strings that happen to contain `es-AR`.

Found while verifying `product-slugs` task 3.1. Pre-existing and unrelated to slugs, but it sits in the exact block that change touched.

## What Changes

- hreflang alternates (`es-AR`, `en-US`, `x-default`) are derived from the page's canonical relative URL with the culture segment **replaced**, not prepended. They can no longer desynchronize from `<link rel="canonical">` because they read the same value.
- One shared helper performs the culture-segment swap; `SetLanguage` stops using `string.Replace` and uses it too, so language switching and hreflang cannot disagree.
- `<html lang>` reflects the active culture instead of a hardcoded `en`.
- Sitemap drops the bare `.../Home/Search` entry (no filters always 302s to Index, and a sitemap loc must be 200).
- Sitemap home entry becomes `/es-AR/Customer/Home/Index`, the form every internal link and the canonical tag already use, removing a second self-canonical URL for the same page.
- Tests become the guard instead of a one-off fix: every sitemap loc is fetched and asserted 200 (not just product locs), plus hreflang reciprocity, `<html lang>`, and language-switch query-preservation coverage.

## Capabilities

### New Capabilities
- `seo/crawl-metadata`: what the site tells crawlers — sitemap contents, canonical URL declaration, and `hreflang`/`lang` language signalling.

### Modified Capabilities
<!-- None. The shipped product-slugs spec pins "absent slug renders 200, no redirect"; this
     change deliberately keeps that and does not touch catalog requirements. -->

## Impact

- `VaultShop.Web/Views/Shared/_Layout.cshtml` — canonical/hreflang computation, `<html lang>`.
- `VaultShop.Web/Controllers/SeoController.cs` — sitemap entry list.
- `VaultShop.Web/Areas/Customer/Controllers/HomeController.cs` — `SetLanguage` culture swap.
- New shared helper in `VaultShop.Utility` plus unit tests.
- `VaultShop.Tests/SeoCanonicalUrlHttpTests.cs` — widened to all locs + new hreflang/lang coverage.
- No payment, auth, order, or catalog behavior changes. No new dependencies. No breaking changes.
