using System.Globalization;

namespace VaultShop.Web.Services.Payments
{
	// ponytail: single source for "this Mercado Pago payment belongs to this order and matches its
	// frozen total" — shared by the cart/admin confirmation syncs and the reconciliation job so the
	// guard cannot drift between them. Fail-closed: missing or unparseable reference/amount → false.
	internal static class MercadoPagoPaymentVerifier
	{
		public static bool Matches(int orderId, decimal orderTotal, PaymentSessionStatusResult status)
		{
			return int.TryParse(status.ExternalReference, NumberStyles.None, CultureInfo.InvariantCulture, out var parsedOrderId)
				&& parsedOrderId == orderId
				&& status.TransactionAmount == orderTotal;
		}
	}
}
