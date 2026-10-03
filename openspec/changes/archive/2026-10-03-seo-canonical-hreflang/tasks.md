## 1. Shared culture-segment helper

- [x] 1.1 Add a pure static helper in `VaultShop.Utility` that replaces the leading culture segment of a relative URL with a target culture, leaving the path remainder and the query string byte-identical, and never touching a culture token that appears anywhere else — verified by unit tests covering culture swap, missing culture segment, query preservation, culture token inside a query value, and empty/root input (`dotnet test --filter <helper>Tests` green)

## 2. Language signalling

- [x] 2.1 Rebuild the `es-AR`/`en-US`/`x-default` alternates in `_Layout.cshtml` from the canonical relative URL by calling the helper, so no alternate can repeat the culture segment and no two alternates collapse onto one URL — verified by rendered-HTML tests asserting the swapped paths, absence of a doubled segment, distinct `es-AR`/`en-US` targets, `x-default` present and pointing at the `es-AR` variant, and preservation of `categoryId`/`cslug`/`keywordId`/`slug` on a filtered page; plus a reciprocity test that fetches each emitted alternate and asserts that page declares the URL it was reached by as one of its own alternates
- [x] 2.2 Make `_Layout.cshtml` declare `lang` from the active culture instead of the hardcoded `en` — verified by tests asserting `lang="es-AR"` on an `es-AR` page and `lang="en-US"` on an `en-US` page
- [x] 2.3 Replace `SetLanguage`'s `string.Replace` culture swap with the shared helper so language switching alters only the culture segment of the path — verified by tests asserting the returned URL keeps the path remainder and the full query string, and that a culture token inside a query value survives unchanged

## 3. Sitemap honesty

- [x] 3.1 Remove the filter-less `.../Home/Search` entry and change the home entry to `/es-AR/Customer/Home/Index` so the sitemap advertises the same URL internal links and the canonical tag use — verified by sitemap assertions plus a fetch of the advertised home URL
- [x] 3.2 Widen `SeoCanonicalUrlHttpTests` so every sitemap loc (not only product locs) is fetched and asserted 200, keeping the well-formed-XML assertion — verified by the widened test passing against a seeded catalog and by it failing if a non-200 entry is reintroduced

## 4. Regression gate

- [x] 4.1 Run `dotnet build VaultShop.sln --no-incremental` warning-free and full `dotnet test VaultShop.sln` green, and re-verify in a browser at 320px and 1280px under `es-AR` and `en-US` that a product page and a filtered page emit valid alternates with no duplicated culture segment and no layout regression — verified by clean build plus the manual pass
