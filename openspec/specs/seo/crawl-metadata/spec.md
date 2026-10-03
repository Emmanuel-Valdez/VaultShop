## Purpose

Defines what the storefront tells search engines about itself: which URLs exist (sitemap), which URL is the authoritative one for a page (canonical), and how the same page is signalled across the `es-AR` and `en-US` cultures (hreflang and document language).

## Requirements

### Requirement: Sitemap lists only reachable canonical URLs

The sitemap SHALL list, for every page it advertises, the canonical URL of that page. Every listed URL SHALL respond with HTTP 200 when fetched directly. URLs that redirect, or that omit the routing segments the site actually serves, SHALL NOT be listed.

#### Scenario: Advertised page URL is reachable
- **WHEN** a crawler fetches every URL listed in the sitemap
- **THEN** each responds with HTTP 200

#### Scenario: Filter-less search endpoint is not advertised
- **WHEN** the sitemap is generated
- **THEN** it contains no entry for the search page without filters, because that URL redirects to the home page

#### Scenario: Home page is listed in the form the site links to
- **WHEN** the sitemap lists the home page
- **THEN** it uses the same URL that internal links and the page's own canonical tag use

#### Scenario: Soft-deleted and unavailable entities are excluded
- **WHEN** the sitemap is generated
- **THEN** it contains no entry for a soft-deleted or unavailable product, nor for a soft-deleted category

### Requirement: Pages declare one canonical URL

Every page SHALL emit exactly one `rel="canonical"` link whose target is the canonical URL of that page. When a page is reachable at more than one URL — for example a product detail page reached without its slug, or a filtered page reached without its slug query parameter — the canonical URL SHALL be the slug-bearing form, not the visited URL.

#### Scenario: Canonical URL matches the sitemap entry
- **WHEN** a product page is rendered
- **THEN** its canonical URL equals the URL the sitemap lists for that product

#### Scenario: Visited URL without a slug still declares the slug canonical
- **WHEN** a shopper reaches a product detail page using the id-only URL
- **THEN** the page renders with HTTP 200 and its canonical tag advertises the slug URL

#### Scenario: Pages without slug semantics keep their visited URL
- **WHEN** a page has no slug-bearing variant
- **THEN** its canonical URL is the URL that was visited

### Requirement: Language alternates point at the same page in the other culture

Every page SHALL emit `hreflang` alternates for `es-AR` and `en-US` plus an `x-default`. Each alternate SHALL target the **same page** expressed in the alternate culture: the culture segment of the canonical URL SHALL be replaced, never prepended, so no alternate contains a repeated culture segment and no alternate contains a repeated path. Query parameters that identify the page SHALL be preserved in every alternate.

#### Scenario: Alternates swap the culture segment
- **WHEN** an `es-AR` page whose canonical path is `/es-AR/Customer/Home/Index` emits alternates
- **THEN** the `en-US` alternate targets `/en-US/Customer/Home/Index` and the `es-AR` alternate targets `/es-AR/Customer/Home/Index`

#### Scenario: Alternates are never duplicated
- **WHEN** any page emits its alternates
- **THEN** no alternate URL contains the culture segment twice

#### Scenario: Alternates preserve page-identifying query parameters
- **WHEN** a filtered page whose canonical URL carries category and collection slug query parameters emits alternates
- **THEN** each alternate carries those same query parameters with only the culture changed

#### Scenario: Alternates are reciprocal
- **WHEN** any page emits alternates, and any of those alternates is itself fetched
- **THEN** that page's alternates include the URL it was reached by

#### Scenario: Alternates never collapse onto one URL
- **WHEN** a page emits alternates
- **THEN** the `es-AR` and `en-US` alternates have different URLs

### Requirement: Document language matches the active culture

The rendered document SHALL declare its language as the active culture, so a page served under `es-AR` SHALL NOT declare itself as English.

#### Scenario: Spanish page declares Spanish
- **WHEN** a page is served under the `es-AR` culture
- **THEN** the document declares `lang="es-AR"`

#### Scenario: English page declares English
- **WHEN** a page is served under the `en-US` culture
- **THEN** the document declares `lang="en-US"`

### Requirement: Language switching preserves the current page

Switching the active culture SHALL return the shopper to the same page — same path after the culture segment, same query string — with only the culture segment changed. A culture-like token appearing elsewhere in the URL, including inside a query parameter value, SHALL NOT be modified.

#### Scenario: Switch preserves path and query
- **WHEN** a shopper switches language from a filtered page whose URL carries both a culture segment and query parameters
- **THEN** the resulting URL has the same path remainder and the same query parameters, with only the culture segment changed

#### Scenario: Switch leaves non-culture tokens alone
- **WHEN** the current query string contains the token of another culture
- **THEN** that token is preserved unchanged in the resulting URL