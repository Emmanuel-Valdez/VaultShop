## Context

See proposal.md for motivation. Current state, all measured against the running app:

- Routing is conventional with exactly two routes (`Program.cs:400` `productDetails`, `Program.cs:405` `default`), the latter `{culture=es-AR}/{area=Customer}/{controller=Home}/{action=Index}/{id?}`. A default value on a segment is **not** optionality — `/es-AR/Home/AboutUs` binds `area=Home, controller=AboutUs` and 404s. `product-slugs` 3.1 already corrected the sitemap's missing area segment; that fix is inherited, not repeated here.
- `_Layout.cshtml:42-44` computes `currentUrl` from `SD.SiteUrl` plus either `ViewData["CanonicalUrl"]` (set by `Details`/`Search` for slug-bearing pages) or `Context.Request.Path`. Lines 61-63 build alternates as `SD.SiteUrl + "/es-AR" + Context.Request.Path`, and `Request.Path` already starts with the culture segment, producing `/es-AR/es-AR/...`.
- `_Layout.cshtml:25` hardcodes `<html lang="en">`.
- `HomeController.SetLanguage` (`:342-353`) swaps culture with `string.Replace("es-AR", culture)` over the whole `returnUrl`, which includes the query string.
- `SeoController.Sitemap` builds every loc by string interpolation. 74 locs: 73 resolve 200, the filter-less `.../Home/Search` 302s (`HomeController.cs:257-261`), and the home entry `/es-AR/` is a second self-canonical URL distinct from the `/es-AR/Customer/Home/Index` that every internal link and the canonical tag use.
- `SeoCanonicalUrlHttpTests` already probes sitemap product locs; the widening in this change is the guard that keeps this class of defect from returning.

Product content does not vary by culture — the storefront's product data is Spanish on both prefixes and only the chrome is translated. `en-US` is a courtesy surface, not a separate market. The store is still in development, so no traffic or backlink history constrains these choices.

## Goals / Non-Goals

**Goals:**
- Alternates cannot disagree with the canonical tag, by construction rather than by discipline.
- One implementation of "swap the culture segment", used by both hreflang and `SetLanguage`.
- A sitemap whose every loc is a 200, asserted automatically.

**Non-Goals:**
- Making `?categoryId=` without `cslug` redirect. `product-slugs` pins absent-slug → 200 for bookmark resilience, and the canonical tag already consolidates the duplicate; a 301 would contradict a shipped requirement for no SEO gain.
- Adding `en-US` URLs to the sitemap. Content is identical across cultures, so listing both would advertise duplicate pages; `es-AR` alone plus hreflang is the honest signal for a courtesy translation.
- Rewriting the sitemap to use `LinkGenerator` instead of string interpolation. That is the root cause of the dead-URL bug, but it is a refactor to schedule when the sitemap is next touched for another reason, not to smuggle into a bug fix.
- Adding `Terms` to the sitemap. It is public and linked in the footer but missing; that is an omission, not a defect.

## Decisions

1. **Derive alternates from the canonical relative URL, replacing the culture segment.**
   Rationale: `currentUrl`'s source is already correct on slug-bearing pages because it comes from `Url.RouteUrl`/`Url.Action`, which include the slug and the page-identifying query params. Swapping the culture on that value makes canonical and alternates share one source of truth, so they cannot drift. Alternatives: (a) use `Request.Path` alone — rejected, it collapses both alternates onto one URL, which *declares* duplicate content, worse than no signal; (b) `Url.Action` with explicit culture per alternate — rejected, it requires enumerating `categoryId`, `cslug`, `keywordId`, `slug`, `searchString`, and anything omitted fails silently; (c) string surgery on `Request.Path` with a `StartsWith` cut — rejected, `Request.Path` drops the query string, so filtered pages would point alternates at the filter-less Search that 302s, breaking exactly the commercially important pages.

2. **Replace the first segment by position, not by `string.Replace`.**
   Rationale: culture is the *first segment of the route*, not an external prefix. `SetLanguage` currently mutates every occurrence anywhere in the URL, so switching language on a search for `es-AR` corrupts the search term. The helper cuts the path at the first `/` boundary and touches nothing else; the query string is carried across untouched.

3. **One shared helper in `VaultShop.Utility`, consumed by the layout and `SetLanguage`.**
   Rationale: two hand-rolled culture swaps in one codebase is the copy-paste bait that produced this defect. Fixing it once where both callers route through is a smaller diff than two correct edits that can diverge again. Pure function, unit tested, no HTTP dependency.

4. **Drop the filter-less `Search` sitemap entry; align the home entry to the linked form.**
   Rationale: a sitemap loc that redirects is not a page. The home entry pointing at `/es-AR/` while every internal link uses `/es-AR/Customer/Home/Index` gives the same content two self-canonical URLs and prioritises the one nothing links to. Aligning the string is the cheap direction; the alternative — making the long form redirect to the short one — would add a redirect hop to the most-visited URL on the site and require regenerating every internal link.

5. **Turn the sitemap test into a standing guarantee: fetch every loc, assert 200.**
   Rationale: the previous dead-URL bug survived a commit that edited this exact list. Asserting the product locs was already the right instinct; widening it to all locs is ~5 lines and makes the invariant impossible to break silently again. The `XDocument.Parse` assertion stays for the same reason — the category loc already emitted a raw `&` once.

6. **`product-slugs` 3.1's area-segment fix is left in place and treated as the baseline.**
   Rationale: this change would be incoherent without it, and it is already covered by an existing assertion, so there is nothing to do beyond not regressing it.

## Risks / Trade-offs

- [Risk] Alternates for free-text search pages (no `categoryId`/`keywordId`) have no slug-bearing canonical, so they fall back to the visited path and their alternates land on the filter-less Search that 302s → Mitigation: accept and record. Those pages are not in the sitemap, and making the free-text search canonical would be a content-duplication decision (which variant is authoritative) that belongs in its own change.
- [Risk] A shared helper becomes a dependency of both a layout and a controller → Mitigation: keep it a pure static string function with no DI and no `HttpContext`; if it ever needs either, the design is wrong.
- [Risk] Widening the sitemap test from product locs to all locs could surface a pre-existing non-200 static page → Mitigation: that is the point; fix the entry rather than weaken the assertion.
- Trade-off: hreflang stays in place even though product content is identical across cultures. Accepted: the rendered page text does differ (chrome is translated), so alternates are a truthful signal, and removing them would forfeit the mechanism rather than fix it.
- Trade-off: `<html lang>` becomes culture-dependent, so a crawler seeing `lang="es-AR"` where it saw `en` is a visible change → Mitigation: correct, and asserted by test.

## Migration Plan

1. Ship helper + layout + `SetLanguage` + sitemap edits together; no schema or data change, no migration.
2. Rollback: revert the commit. Nothing persisted, nothing to reverse.
3. Post-deploy: fetch `/sitemap.xml`, confirm every loc is 200, and spot-check the alternates on one product page in both cultures.
