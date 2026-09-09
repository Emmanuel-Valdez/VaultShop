using System.Globalization;
using System.Net;
using Microsoft.Extensions.Options;
using VaultShop.DataAccess.Repository.IRepository;
using VaultShop.Models;
using VaultShop.Utility;
using VaultShop.Web.Services.Email;

namespace VaultShop.Web.Services.Payments;

/// <summary>
/// Safety-net background job that re-verifies stale pending provider orders
/// (Stripe / Mercado Pago) against the provider API and promotes them to paid
/// when the provider reports them as paid. Idempotent: relies on
/// <see cref="IPaymentStatusService.MarkCheckoutSessionPaid"/> for state
/// transitions and email guards, mirroring the webhook paths.
/// </summary>
public sealed class PaymentReconciliationBackgroundService : BackgroundService
{
	private readonly PaymentReconciliationOptions _options;
	private readonly IServiceProvider _services;
	private readonly IConfiguration _configuration;
	private readonly ILogger<PaymentReconciliationBackgroundService> _logger;

	public PaymentReconciliationBackgroundService(
		IOptions<PaymentReconciliationOptions> options,
		IServiceProvider services,
		IConfiguration configuration,
		ILogger<PaymentReconciliationBackgroundService> logger)
	{
		_options = options.Value;
		_services = services;
		_configuration = configuration;
		_logger = logger;
	}

	protected override async Task ExecuteAsync(CancellationToken stoppingToken)
	{
		if (!_options.Enabled)
		{
			_logger.LogInformation("Payment reconciliation is disabled; background service will not run.");
			return;
		}

		_logger.LogInformation(
			"Payment reconciliation started. Interval={Interval} StaleAfter={StaleAfter} MaxAge={MaxAge} BatchSize={BatchSize}",
			_options.Interval, _options.StaleAfter, _options.MaxAge, _options.BatchSize);

		using var timer = new PeriodicTimer(_options.Interval);
		while (await timer.WaitForNextTickAsync(stoppingToken))
		{
			try
			{
				await ReconcileOnceAsync(stoppingToken);
			}
			catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
			{
				throw;
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Unexpected error during payment reconciliation cycle.");
			}
		}
	}

	private async Task ReconcileOnceAsync(CancellationToken stoppingToken)
	{
		using var scope = _services.CreateScope();
		await ReconcileScopeAsync(scope.ServiceProvider, stoppingToken);
	}

	internal async Task ReconcileScopeAsync(IServiceProvider scopeServices, CancellationToken stoppingToken)
	{
		var unitOfWork = scopeServices.GetRequiredService<IUnitOfWork>();
		var paymentStatusService = scopeServices.GetRequiredService<IPaymentStatusService>();
		var emailService = scopeServices.GetRequiredService<ITransactionalEmailService>();

		var stripeEnabled = _configuration.GetValue("Payments:StripeEnabled", true);
		var mercadoPagoEnabled = _configuration.GetValue("Payments:MercadoPagoEnabled", false);

		var orders = GetStaleBatch(unitOfWork, stripeEnabled, mercadoPagoEnabled).ToList();
		_logger.LogInformation("Payment reconciliation cycle started. BatchSize={BatchSize}.", orders.Count);

		var reconciled = 0;
		foreach (var order in orders)
		{
			if (stoppingToken.IsCancellationRequested)
			{
				break;
			}

			var providerEnabled = order.PaymentMethod == SD.PaymentMethodStripe
				? stripeEnabled
				: order.PaymentMethod == SD.PaymentMethodMercadoPago
					? mercadoPagoEnabled
					: false;
			if (!providerEnabled)
			{
				_logger.LogDebug("Skipping order {OrderId} for disabled provider {PaymentMethod}.", order.Id, order.PaymentMethod);
				continue;
			}

			try
			{
				if (await TryReconcileOrderAsync(scopeServices, order, paymentStatusService, emailService))
				{
					reconciled++;
				}
			}
			catch (OperationCanceledException)
			{
				throw;
			}
			catch (HttpRequestException ex)
			{
				_logger.LogWarning(ex, "Provider request failed for order {OrderId} ({PaymentMethod}); will retry next cycle.", order.Id, order.PaymentMethod);
			}
			catch (TimeoutException ex)
			{
				_logger.LogWarning(ex, "Provider request timed out for order {OrderId} ({PaymentMethod}); will retry next cycle.", order.Id, order.PaymentMethod);
			}
			catch (Exception ex)
			{
				_logger.LogWarning(ex, "Unexpected error reconciling order {OrderId} ({PaymentMethod}); will retry next cycle.", order.Id, order.PaymentMethod);
			}
		}

		_logger.LogInformation("Payment reconciliation cycle finished. Reconciled={Reconciled}.", reconciled);
	}

	private IEnumerable<OrderHeader> GetStaleBatch(
		IUnitOfWork unitOfWork, bool stripeEnabled, bool mercadoPagoEnabled)
	{
		var now = DateTime.UtcNow;

		if (!stripeEnabled && !mercadoPagoEnabled)
		{
			return Array.Empty<OrderHeader>();
		}

		var providers = new List<string>(2);
		if (stripeEnabled) providers.Add(SD.PaymentMethodStripe);
		if (mercadoPagoEnabled) providers.Add(SD.PaymentMethodMercadoPago);

		var minAge = now - _options.MaxAge;
		var staleBefore = now - _options.StaleAfter;

		return unitOfWork.OrderHeader.GetAll(order =>
				order.PaymentStatus == SD.PaymentStatusPending &&
				order.PaymentMethod != null &&
				providers.Contains(order.PaymentMethod) &&
				order.SessionId != null &&
				order.OrderDate >= minAge &&
				order.OrderDate <= staleBefore &&
				order.OrderStatus != SD.StatusCancelled &&
				order.OrderStatus != SD.StatusRefunded &&
				order.PaymentStatus != SD.PaymentStatusRejected)
			.OrderBy(order => order.OrderDate)
			.Take(_options.BatchSize)
			.ToList();
	}

	private async Task<bool> TryReconcileOrderAsync(
		IServiceProvider serviceProvider,
		OrderHeader order,
		IPaymentStatusService paymentStatusService,
		ITransactionalEmailService emailService)
	{
		var paymentSessionService = serviceProvider.GetRequiredKeyedService<IPaymentSessionService>(order.PaymentMethod!);
		var status = paymentSessionService.GetCheckoutSessionStatus(order.SessionId!, null);

		if (!status.IsPaid)
		{
			_logger.LogDebug("Order {OrderId} ({PaymentMethod}) is still not paid at provider; remaining pending.", order.Id, order.PaymentMethod);
			return false;
		}

		if (order.PaymentMethod == SD.PaymentMethodMercadoPago && !MercadoPagoResultMatches(order, status))
		{
			_logger.LogWarning(
				"Mercado Pago result for order {OrderId} does not match stored preference; leaving pending (ExternalReference={ExternalReference} TransactionAmount={TransactionAmount}).",
				order.Id, status.ExternalReference, status.TransactionAmount);
			return false;
		}

		var markedPaid = await paymentStatusService.MarkCheckoutSessionPaid(
			new PaymentSessionStatusUpdate(order.Id, order.SessionId!, status.PaymentIntentId));

		if (markedPaid)
		{
			await emailService.TrySendPaymentReceiptAsync(order.Id);
			_logger.LogInformation(
				"Reconciled order {OrderId} ({PaymentMethod}) to paid via provider check. SessionId={SessionId}.",
				order.Id, order.PaymentMethod, order.SessionId);
			return true;
		}

		_logger.LogDebug("Order {OrderId} was not promoted by reconciliation ({PaymentMethod}); no change.", order.Id, order.PaymentMethod);
		return false;
	}

	private static bool MercadoPagoResultMatches(OrderHeader order, PaymentSessionStatusResult status)
	{
		if (!int.TryParse(status.ExternalReference, NumberStyles.None, CultureInfo.InvariantCulture, out var orderId))
		{
			return false;
		}

		return orderId == order.Id && status.TransactionAmount == order.OrderTotal;
	}
}
