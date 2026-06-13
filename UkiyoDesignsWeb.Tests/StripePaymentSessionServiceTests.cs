using Stripe.Checkout;
using UkiyoDesignsWeb.Services.Payments;

namespace UkiyoDesignsWeb.Tests
{
	public class StripePaymentSessionServiceTests
	{
		[Fact]
		public void CreateCheckoutSession_BuildsExpectedStripeLineItemsWithoutCallingRealStripe()
		{
			var stripeClient = new CapturingStripeCheckoutSessionClient();
			var service = new StripePaymentSessionService(stripeClient);
			var request = new PaymentSessionRequest(
				OrderId: 42,
				LineItems:
				[
					new PaymentSessionLineItem("Kimono", 100.50m, 2),
					new PaymentSessionLineItem("Obi Belt", 25m, 1)
				],
				SuccessUrl: "https://example.test/success",
				CancelUrl: "https://example.test/cancel");

			var result = service.CreateCheckoutSession(request);

			Assert.Equal("cs_test_123", result.SessionId);
			Assert.Equal("pi_test_123", result.PaymentIntentId);
			Assert.Equal("https://stripe.test/checkout", result.Url);
			var options = Assert.IsType<SessionCreateOptions>(stripeClient.CapturedOptions);
			Assert.Equal("payment", options.Mode);
			Assert.Equal("https://example.test/success", options.SuccessUrl);
			Assert.Equal("https://example.test/cancel", options.CancelUrl);
			Assert.Equal("42", options.Metadata["orderId"]);
			Assert.Collection(options.LineItems,
				first =>
				{
					Assert.Equal("Kimono", first.PriceData.ProductData.Name);
					Assert.Equal(10050, first.PriceData.UnitAmount);
					Assert.Equal("usd", first.PriceData.Currency);
					Assert.Equal(2, first.Quantity);
				},
				second =>
				{
					Assert.Equal("Obi Belt", second.PriceData.ProductData.Name);
					Assert.Equal(2500, second.PriceData.UnitAmount);
					Assert.Equal("usd", second.PriceData.Currency);
					Assert.Equal(1, second.Quantity);
				});
		}

		private sealed class CapturingStripeCheckoutSessionClient : IStripeCheckoutSessionClient
		{
			public SessionCreateOptions? CapturedOptions { get; private set; }

			public Session Create(SessionCreateOptions options)
			{
				CapturedOptions = options;
				return new Session
				{
					Id = "cs_test_123",
					PaymentIntentId = "pi_test_123",
					Url = "https://stripe.test/checkout"
				};
			}
		}
	}
}
