using VaultShop.Models;
using VaultShop.Models.ViewModels;

namespace VaultShop.Web.Services.Checkout
{
	public interface ICheckoutService
	{
		CheckoutSummaryResult BuildSummary(string userId, bool useWholesalePrice);
		CheckoutCreateOrderResult CreateOrder(string userId, OrderHeader postedOrderHeader, bool useWholesalePrice);

		public sealed class CheckoutSummaryResult
		{
			public bool IsAuthorized { get; init; } = true;
			public bool IsCartEmpty { get; init; }
			public bool ShouldBlockUser { get; init; }
			public ShoppingCartVM? ShoppingCartVM { get; init; }
			public ApplicationUser? ApplicationUser { get; init; }
		}

		public sealed class CheckoutCreateOrderResult
		{
			public bool IsAuthorized { get; init; } = true;
			public bool IsCartEmpty { get; init; }
			public bool ShouldBlockUser { get; init; }
			public bool OrderTotalInvalid { get; init; }
			public int? OrderId { get; init; }
			public bool RequiresOnlinePayment { get; init; }
			public ShoppingCartVM? ShoppingCartVM { get; init; }
			public ApplicationUser? ApplicationUser { get; init; }
		}
	}
}
