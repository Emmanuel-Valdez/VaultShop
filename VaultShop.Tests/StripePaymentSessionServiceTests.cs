using Stripe.Checkout;
using VaultShop.Web.Services.Payments;

namespace VaultShop.Web.Tests
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

		[Fact]
		public void GetCheckoutSessionStatus_ReturnsStripePaymentStatusWithoutApprovingRedirect()
		{
			var stripeClient = new CapturingStripeCheckoutSessionClient
			{
				SessionToReturn = new Session
				{
					Id = "cs_test_paid",
					PaymentIntentId = "pi_test_paid",
					PaymentStatus = "paid"
				}
			};
			var service = new StripePaymentSessionService(stripeClient);

			var result = service.GetCheckoutSessionStatus("cs_test_paid");

			Assert.Equal("cs_test_paid", stripeClient.RequestedSessionId);
			Assert.Equal("cs_test_paid", result.SessionId);
			Assert.Equal("pi_test_paid", result.PaymentIntentId);
			Assert.Equal("paid", result.PaymentStatus);
			Assert.True(result.IsPaid);
		}

		[Fact]
		public void ExpireCheckoutSession_ExpiresStoredStripeSession()
		{
			var stripeClient = new CapturingStripeCheckoutSessionClient();
			var service = new StripePaymentSessionService(stripeClient);

			service.ExpireCheckoutSession("cs_test_expire");

			Assert.Equal("cs_test_expire", stripeClient.ExpiredSessionId);
		}

		private sealed class CapturingStripeCheckoutSessionClient : IStripeCheckoutSessionClient
		{
			public SessionCreateOptions? CapturedOptions { get; private set; }
			public string? RequestedSessionId { get; private set; }
			public string? ExpiredSessionId { get; private set; }
			public Session? SessionToReturn { get; init; }

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

			public Session Get(string sessionId)
			{
				RequestedSessionId = sessionId;
				return SessionToReturn ?? new Session { Id = sessionId };
			}

			public Session Expire(string sessionId)
			{
				ExpiredSessionId = sessionId;
				return new Session { Id = sessionId };
			}
		}
	}
}
