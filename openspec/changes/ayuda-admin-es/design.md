## Context

See proposal.md for why. Help is hardcoded Spanish in `Home/Help.cshtml` plus `_HelpProducts`, `_HelpCosts`, and `_HelpFinalPrices`. It still tells the admin to set monthly expectation on the category. Category upsert now has slug, image, and average shipping cost. Expectation, stock, availability, and the offer window live on the product. Promotions, coupons, variants, and collections have no tab.

## Goals / Non-Goals

**Goals:**

- One Spanish help page whose tabs match the admin the operator actually uses.
- Keep the existing tab partial pattern. No new CMS.

**Non-Goals:**

- English help copy.
- Changing admin forms, pricing, or checkout.
- A timed cart sweeper or a per-customer coupon cap. If help mentions coupon max uses, it must say the cap is global across orders, not per customer.

## Decisions

1. Add tabs in `Help.cshtml` and one partial per new topic: offers, promotions, coupons, variants, collections. Rewrite the product and category sections in `_HelpProducts`. Touch `_HelpCosts` and `_HelpFinalPrices` only where they still say expectation or shipping belongs to the wrong entity.
2. Write the body in Spanish inside the partials, as today. Do not move this page to resx just to satisfy localization. The spec allows Spanish body under `en-US`.
3. Apply this change after `oferta-decimales-variantes`, so the offer and promotion sections describe persisted offers and the localized select labels.

## Risks / Trade-offs

- [Help drifts again the next time admin changes] → Keep each tab to the operations in the current forms. Do not document planned jobs.
- [English visitors see Spanish help] → Accepted. English copy is a later change.

## Migration Plan

View-only. No migration. Rollback is reverting the partials.

## Open Questions

None.
