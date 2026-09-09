using System.Linq.Expressions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using VaultShop.DataAccess.Repository.IRepository;
using VaultShop.Models;
using VaultShop.Utility;
using VaultShop.Web.Services.Email;
using VaultShop.Web.Services.Payments;

namespace VaultShop.Web.Tests;

public class PaymentReconciliationTests
{
	private readonly PaymentReconciliationOptions _options = new()
	{
		Enabled = true,
		Interval = TimeSpan.FromMinutes(10),
		StaleAfter = TimeSpan.FromMinutes(5),
		MaxAge = TimeSpan.FromHours(48),
		BatchSize = 20
	};

	[Fact]
	public async Task Reconcile_PaidStripeOrder_PromotesAndSendsReceiptOnce()
	{
		var order = StaleOrder(id: 1, SD.PaymentMethodStripe, total: 100m);
		var environment = CreateEnvironment([order]);
		environment.StripeSession.Setup(s => s.GetCheckoutSessionStatus("cs_1", null))
			.Returns(new PaymentSessionStatusResult("cs_1", "pi_1", "paid"));
		environment.PaymentStatus.Setup(s => s.MarkCheckoutSessionPaid(It.IsAny<PaymentSessionStatusUpdate>()))
			.ReturnsAsync(true);

		await environment.Service.ReconcileScopeAsync(environment.Provider, CancellationToken.None);

		environment.PaymentStatus.Verify(s => s.MarkCheckoutSessionPaid(
			It.Is<PaymentSessionStatusUpdate>(u => u.OrderId == 1 && u.SessionId == "cs_1" && u.PaymentIntentId == "pi_1")), Times.Once);
		environment.Email.Verify(s => s.TrySendPaymentReceiptAsync(1), Times.Once);
	}

	[Fact]
	public async Task Reconcile_UnpaidOrder_RemainsPendingAndNoReceipt()
	{
		var order = StaleOrder(id: 1, SD.PaymentMethodStripe, total: 100m);
		var environment = CreateEnvironment([order]);
		environment.StripeSession.Setup(s => s.GetCheckoutSessionStatus("cs_1", null))
			.Returns(new PaymentSessionStatusResult("cs_1", null, "unpaid"));

		await environment.Service.ReconcileScopeAsync(environment.Provider, CancellationToken.None);

		environment.PaymentStatus.Verify(s => s.MarkCheckoutSessionPaid(It.IsAny<PaymentSessionStatusUpdate>()), Times.Never);
		environment.Email.Verify(s => s.TrySendPaymentReceiptAsync(It.IsAny<int>()), Times.Never);
	}

	[Fact]
	public async Task Reconcile_AlreadyPaidIsNoOp()
	{
		var order = StaleOrder(id: 1, SD.PaymentMethodStripe, total: 100m);
		var environment = CreateEnvironment([order]);
		environment.StripeSession.Setup(s => s.GetCheckoutSessionStatus("cs_1", null))
			.Returns(new PaymentSessionStatusResult("cs_1", "pi_1", "paid"));
		// PaymentStatusService returns true for already-paid (idempotent no-op).
		environment.PaymentStatus.Setup(s => s.MarkCheckoutSessionPaid(It.IsAny<PaymentSessionStatusUpdate>()))
			.ReturnsAsync(true);

		await environment.Service.ReconcileScopeAsync(environment.Provider, CancellationToken.None);

		// No receipt re-send beyond the single call after promotion guard.
		environment.PaymentStatus.Verify(s => s.MarkCheckoutSessionPaid(It.IsAny<PaymentSessionStatusUpdate>()), Times.Once);
	}

	[Fact]
	public async Task Reconcile_StripeNotPaidNoReceipt()
	{
		var order = StaleOrder(id: 1, SD.PaymentMethodStripe, total: 100m);
		var environment = CreateEnvironment([order]);
		environment.StripeSession.Setup(s => s.GetCheckoutSessionStatus("cs_1", null))
			.Returns(new PaymentSessionStatusResult("cs_1", null, "unpaid"));

		await environment.Service.ReconcileScopeAsync(environment.Provider, CancellationToken.None);

		environment.PaymentStatus.Verify(s => s.MarkCheckoutSessionPaid(It.IsAny<PaymentSessionStatusUpdate>()), Times.Never);
	}

	[Fact]
	public async Task Reconcile_MercadoPagoMismatchedAmountStaysPending()
	{
		var order = StaleOrder(id: 1, SD.PaymentMethodMercadoPago, total: 100m);
		var environment = CreateEnvironment([order]);
		environment.MercadoPagoSession.Setup(s => s.GetCheckoutSessionStatus("pref_1", null))
			.Returns(new PaymentSessionStatusResult("pref_1", "PAY123", "paid", "1", 99m));

		await environment.Service.ReconcileScopeAsync(environment.Provider, CancellationToken.None);

		environment.PaymentStatus.Verify(s => s.MarkCheckoutSessionPaid(It.IsAny<PaymentSessionStatusUpdate>()), Times.Never);
		environment.Email.Verify(s => s.TrySendPaymentReceiptAsync(It.IsAny<int>()), Times.Never);
	}

	[Fact]
	public async Task Reconcile_MercadoPagoMismatchedReferenceStaysPending()
	{
		var order = StaleOrder(id: 1, SD.PaymentMethodMercadoPago, total: 100m);
		var environment = CreateEnvironment([order]);
		environment.MercadoPagoSession.Setup(s => s.GetCheckoutSessionStatus("pref_1", null))
			.Returns(new PaymentSessionStatusResult("pref_1", "PAY123", "paid", "999", 100m));

		await environment.Service.ReconcileScopeAsync(environment.Provider, CancellationToken.None);

		environment.PaymentStatus.Verify(s => s.MarkCheckoutSessionPaid(It.IsAny<PaymentSessionStatusUpdate>()), Times.Never);
	}

	[Fact]
	public async Task Reconcile_MercadoPagoMatchingPromotes()
	{
		var order = StaleOrder(id: 1, SD.PaymentMethodMercadoPago, total: 100m);
		var environment = CreateEnvironment([order]);
		environment.MercadoPagoSession.Setup(s => s.GetCheckoutSessionStatus("pref_1", null))
			.Returns(new PaymentSessionStatusResult("pref_1", "PAY123", "paid", "1", 100m));
		environment.PaymentStatus.Setup(s => s.MarkCheckoutSessionPaid(It.IsAny<PaymentSessionStatusUpdate>()))
			.ReturnsAsync(true);

		await environment.Service.ReconcileScopeAsync(environment.Provider, CancellationToken.None);

		environment.PaymentStatus.Verify(s => s.MarkCheckoutSessionPaid(
			It.Is<PaymentSessionStatusUpdate>(u => u.OrderId == 1 && u.PaymentIntentId == "PAY123")), Times.Once);
		environment.Email.Verify(s => s.TrySendPaymentReceiptAsync(1), Times.Once);
	}

	[Fact]
	public async Task Reconcile_DisabledProviderIsSkipped()
	{
		var options = new PaymentReconciliationOptions
		{
			Enabled = true,
			Interval = TimeSpan.FromMinutes(10),
			StaleAfter = TimeSpan.FromMinutes(5),
			MaxAge = TimeSpan.FromHours(48),
			BatchSize = 20
		};
		// Stripe disabled entirely, MP not enabled => no orders selected.
		var order = StaleOrder(id: 1, SD.PaymentMethodStripe, total: 100m);
		var environment = CreateEnvironment([order], options, stripeEnabled: false, mercadoPagoEnabled: false);

		await environment.Service.ReconcileScopeAsync(environment.Provider, CancellationToken.None);

		environment.PaymentStatus.Verify(s => s.MarkCheckoutSessionPaid(It.IsAny<PaymentSessionStatusUpdate>()), Times.Never);
	}

	[Fact]
	public async Task Reconcile_FailingOrderDoesNotAbortBatch()
	{
		var okOrder = StaleOrder(id: 1, SD.PaymentMethodStripe, total: 100m);
		var failingOrder = StaleOrder(id: 2, SD.PaymentMethodStripe, total: 200m, sessionId: "cs_FAIL");
		var environment = CreateEnvironment([failingOrder, okOrder]);
		environment.StripeSession.Setup(s => s.GetCheckoutSessionStatus("cs_FAIL", null))
			.Throws(new HttpRequestException("provider down"));
		environment.StripeSession.Setup(s => s.GetCheckoutSessionStatus("cs_1", null))
			.Returns(new PaymentSessionStatusResult("cs_1", "pi_1", "paid"));
		environment.PaymentStatus.Setup(s => s.MarkCheckoutSessionPaid(It.IsAny<PaymentSessionStatusUpdate>()))
			.ReturnsAsync(true);

		await environment.Service.ReconcileScopeAsync(environment.Provider, CancellationToken.None);

		// Successful order still reconciled despite the failing one.
		environment.PaymentStatus.Verify(s => s.MarkCheckoutSessionPaid(
			It.Is<PaymentSessionStatusUpdate>(u => u.OrderId == 1)), Times.Once);
		environment.Email.Verify(s => s.TrySendPaymentReceiptAsync(1), Times.Once);
	}

	[Fact]
	public async Task Reconcile_BankTransferOrderIsNeverSelected()
	{
		var bankOrder = StaleOrder(id: 1, SD.PaymentMethodBankTransfer, total: 100m);
		var environment = CreateEnvironment([bankOrder]);

		await environment.Service.ReconcileScopeAsync(environment.Provider, CancellationToken.None);

		environment.PaymentStatus.Verify(s => s.MarkCheckoutSessionPaid(It.IsAny<PaymentSessionStatusUpdate>()), Times.Never);
	}

	[Fact]
	public async Task Reconcile_TerminalOrderIsExcludedFromSelection()
	{
		var cancelled = StaleOrder(id: 1, SD.PaymentMethodStripe, total: 100m);
		cancelled.OrderStatus = SD.StatusCancelled;
		var environment = CreateEnvironment([cancelled]);

		await environment.Service.ReconcileScopeAsync(environment.Provider, CancellationToken.None);

		environment.PaymentStatus.Verify(s => s.MarkCheckoutSessionPaid(It.IsAny<PaymentSessionStatusUpdate>()), Times.Never);
		environment.StripeSession.Verify(s => s.GetCheckoutSessionStatus(It.IsAny<string>(), null), Times.Never);
	}

	[Fact]
	public async Task Reconcile_TooFreshAndTooOldOrdersExcluded()
	{
		var tooFresh = StaleOrder(id: 1, SD.PaymentMethodStripe, total: 100m, age: TimeSpan.FromSeconds(30));
		var tooOld = StaleOrder(id: 2, SD.PaymentMethodStripe, total: 100m, age: TimeSpan.FromHours(96));
		var environment = CreateEnvironment([tooFresh, tooOld]);

		await environment.Service.ReconcileScopeAsync(environment.Provider, CancellationToken.None);

		environment.StripeSession.Verify(s => s.GetCheckoutSessionStatus(It.IsAny<string>(), null), Times.Never);
		environment.PaymentStatus.Verify(s => s.MarkCheckoutSessionPaid(It.IsAny<PaymentSessionStatusUpdate>()), Times.Never);
	}

	[Fact]
	public async Task Reconcile_BatchSizeCapsOrdersProcessed()
	{
		var options = new PaymentReconciliationOptions
		{
			Enabled = true,
			Interval = TimeSpan.FromMinutes(10),
			StaleAfter = TimeSpan.FromMinutes(5),
			MaxAge = TimeSpan.FromHours(48),
			BatchSize = 2
		};
		var orders = Enumerable.Range(1, 5)
			.Select(i => StaleOrder(id: i, SD.PaymentMethodStripe, total: 100m))
			.ToList();
		var environment = CreateEnvironment(orders, options);

		await environment.Service.ReconcileScopeAsync(environment.Provider, CancellationToken.None);

		// Only 2 orders in the batch -> at most 2 provider lookups.
		environment.StripeSession.Verify(s => s.GetCheckoutSessionStatus(It.IsAny<string>(), null), Times.Exactly(2));
	}

	[Fact]
	public async Task Reconcile_SessionRequired_NoSessionOrdersSkipped()
	{
		var noSession = StaleOrder(id: 1, SD.PaymentMethodStripe, total: 100m);
		noSession.SessionId = null;
		var environment = CreateEnvironment([noSession]);

		await environment.Service.ReconcileScopeAsync(environment.Provider, CancellationToken.None);

		environment.StripeSession.Verify(s => s.GetCheckoutSessionStatus(It.IsAny<string>(), null), Times.Never);
	}

	private static OrderHeader StaleOrder(
		int id,
		string paymentMethod,
		decimal total,
		string? sessionId = null,
		TimeSpan? age = null)
	{
		return new OrderHeader
		{
			Id = id,
			PaymentMethod = paymentMethod,
			PaymentStatus = SD.PaymentStatusPending,
			OrderStatus = SD.StatusPending,
			SessionId = sessionId ?? (paymentMethod == SD.PaymentMethodStripe ? $"cs_{id}" : $"pref_{id}"),
			OrderTotal = total,
			OrderDate = DateTime.UtcNow - (age ?? TimeSpan.FromHours(6))
		};
	}

	private Environment CreateEnvironment(
		IEnumerable<OrderHeader> orders,
		PaymentReconciliationOptions? options = null,
		bool stripeEnabled = true,
		bool mercadoPagoEnabled = true)
	{
		options ??= _options;
		var orderList = orders.ToList();

		var orderRepository = new Mock<IOrderHeaderRepository>();
		orderRepository.Setup(r => r.GetAll(
				It.IsAny<Expression<Func<OrderHeader, bool>>>(),
				It.IsAny<string?>(),
				It.IsAny<bool>()))
			.Returns((Expression<Func<OrderHeader, bool>> filter, string? _, bool _) =>
				orderList.Where(filter.Compile()).ToList());

		var unitOfWorkMock = new Mock<IUnitOfWork>();
		unitOfWorkMock.Setup(u => u.OrderHeader).Returns(orderRepository.Object);
		var paymentStatusMock = new Mock<IPaymentStatusService>();
		var emailMock = new Mock<ITransactionalEmailService>();

		var services = new ServiceCollection();
		services.AddSingleton<IUnitOfWork>(unitOfWorkMock.Object);
		services.AddSingleton<IPaymentStatusService>(paymentStatusMock.Object);
		services.AddSingleton<ITransactionalEmailService>(emailMock.Object);

		var stripeSession = new Mock<IPaymentSessionService>();
		var mercadoPagoSession = new Mock<IPaymentSessionService>();
		services.AddKeyedSingleton(SD.PaymentMethodStripe, stripeSession.Object);
		services.AddKeyedSingleton(SD.PaymentMethodMercadoPago, mercadoPagoSession.Object);
		var provider = services.BuildServiceProvider();

		var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
		{
			["Payments:StripeEnabled"] = stripeEnabled.ToString(),
			["Payments:MercadoPagoEnabled"] = mercadoPagoEnabled.ToString()
		}).Build();

		var service = new PaymentReconciliationBackgroundService(
			Options.Create(options),
			Mock.Of<IServiceProvider>(),
			configuration,
			NullLogger<PaymentReconciliationBackgroundService>.Instance);

		return new Environment(service, provider, stripeSession, mercadoPagoSession, paymentStatusMock, emailMock);
	}

	private sealed record Environment(
		PaymentReconciliationBackgroundService Service,
		IServiceProvider Provider,
		Mock<IPaymentSessionService> StripeSession,
		Mock<IPaymentSessionService> MercadoPagoSession,
		Mock<IPaymentStatusService> PaymentStatus,
		Mock<ITransactionalEmailService> Email);
}
