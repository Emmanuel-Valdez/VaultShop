## Why

The payment-method discount is only evaluated when the method is known — at `CreateOrder` (`CheckoutService.cs:214-215`) — never on the Summary GET (`CartController.cs:167` calls `BuildSummary` without a method). The Stripe/MP session is built from `PaymentSessionLineItem(item.Product.Name, item.Price, item.Count)` (`CartController.cs:365-367`), where `item.Price` is post-specific-discounts but pre-payment-discount (that discount lives only at header level: `DiscountEvaluation.Total = SubtotalBase - SpecificDiscountTotal - PaymentDiscountTotal`, `IDiscountEvaluator.cs:82`). Consequence: card/MP checkouts charge more than the order total the system records, while bank-transfer shoppers confirm blind. The picker (`_PaymentMethodPicker.cshtml`) never advertises the discount and `Summary.cshtml:106-112` carries the discount row but always receives 0.

## What Changes

- Money fix: the payment session SHALL charge `OrderTotal`. `PaymentDiscountTotal` is prorated across lines for the session request only (same technique as `ProrateCoupon` in `DiscountEvaluator`), without persisting anything; `OrderDetail` rows and the header stay intact.
- Pre-confirmation visibility: (a) the picker announces the discount on the method card (e.g. "−10% paying by bank transfer", computed from active promotions); (b) choosing a method on the Summary triggers an AJAX re-evaluation that updates the discount row and the final total BEFORE Place Order; (c) a note under the selected method states that method's current offer before paying.
- FAQ: new Q11 accordion item in `Areas/Customer/Views/Home/FAQs.cshtml` plus `Question11`/`Answer11` keys in `FAQs.{es,en}.resx` (following the existing 10-pair pattern): specific discounts do not stack (best price wins), the payment-method discount does stack on top — which is what the evaluator does (`DiscountEvaluator.cs:27-44,86-104`).
- Out of scope: any change to evaluator stacking, persisted breakdowns, or session providers.

## Capabilities

### New Capabilities

- None.

### Modified Capabilities

- `pricing/discounts`: payment sessions charge the order total; the shopper sees the payment-method discount before confirming; the FAQ documents stacking.
- `order-lifecycle`: the charged session amount equals the frozen order total.

## Impact

- `Areas/Customer/Controllers/CartController.cs`: session line items prorated to `OrderTotal`; new AJAX re-evaluation endpoint reusing `BuildSummary(userId, wholesale, coupon, method)`.
- `Views/Shared/_PaymentMethodPicker.cshtml` (+ `PaymentMethodPickerVM` discount-announcement fields) and `Areas/Customer/Views/Cart/Summary.cshtml` (+ method-change script, selected-method note).
- `Areas/Customer/Views/Home/FAQs.cshtml` + `FAQs.{es,en}.resx` (`Question11`/`Answer11`).
- No evaluator, provider, or schema change. No migration.
