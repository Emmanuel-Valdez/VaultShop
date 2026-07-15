using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using VaultShop.DataAccess.Repository.IRepository;
using VaultShop.Models;
using VaultShop.Utility;
using VaultShop.Web.Services.Branding;

namespace VaultShop.Web.Services.Email;

public sealed class TransactionalEmailService : ITransactionalEmailService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IEmailSender _emailSender;
    private readonly BrandingOptions _branding;
    private readonly ILogger<TransactionalEmailService> _logger;
    private readonly string? _adminEmail;
    private readonly string _bankTransferCbu;
    private readonly string _bankTransferAlias;
    private readonly string _bankTransferRecipientName;
    private readonly string _bankTransferBankName;

    public TransactionalEmailService(
        IUnitOfWork unitOfWork,
        IEmailSender emailSender,
        IOptions<BrandingOptions> branding,
        IConfiguration configuration,
        ILogger<TransactionalEmailService> logger)
    {
        _unitOfWork = unitOfWork;
        _emailSender = emailSender;
        _branding = branding.Value;
        _logger = logger;
        _adminEmail = configuration["Email:AdminEmail"];
        _bankTransferCbu = configuration["Payments:BankTransferCbu"] ?? string.Empty;
        _bankTransferAlias = configuration["Payments:BankTransferAlias"] ?? string.Empty;
        _bankTransferRecipientName = configuration["Payments:BankTransferRecipientName"] ?? string.Empty;
        _bankTransferBankName = configuration["Payments:BankTransferBankName"] ?? string.Empty;
    }

    public async Task TrySendOrderConfirmationAsync(int orderId)
    {
        var order = _unitOfWork.OrderHeader.Get(
            o => o.Id == orderId,
            includeProperties: "ApplicationUser",
            tracked: true);
        if (order is null) return;

        if (order.OrderConfirmationEmailSentUtc.HasValue)
        {
            _logger.LogInformation("Order confirmation already sent for order {OrderId} at {SentAt}, skipping.", orderId, order.OrderConfirmationEmailSentUtc);
            return;
        }

        var details = _unitOfWork.OrderDetail
            .GetAll(d => d.OrderHeaderId == orderId, includeProperties: "Product")
            .ToList();

        var items = details.Select(d => new OrderItemLine(
            d.Product?.Name ?? $"Product #{d.ProductId}",
            d.Count,
            d.Price.ToString("C")));

        var total = order.OrderTotal.ToString("C");
        var userEmail = order.ApplicationUser?.Email;
        if (string.IsNullOrWhiteSpace(userEmail)) return;

        var includeBankTransferInstructions = order.PaymentMethod == SD.PaymentMethodBankTransfer &&
            (order.PaymentStatus == SD.PaymentStatusPending || order.PaymentStatus == SD.PaymentStatusDelayedPayment);

        var content = EmailTemplates.OrderConfirmation(
            _branding.PublicName,
            order.Name,
            order.Id,
            items,
            total,
            SD.SiteUrl,
            Thread.CurrentThread.CurrentUICulture,
            order.PaymentMethod,
            includeBankTransferInstructions,
            _bankTransferCbu,
            _bankTransferAlias,
            _bankTransferRecipientName,
            _bankTransferBankName);

        await TrySendEmailAsync(orderId, userEmail, content,
            () => order.OrderConfirmationEmailSentUtc = DateTime.UtcNow,
            "order confirmation");
    }

    public async Task TrySendPaymentReceiptAsync(int orderId)
    {
        var order = _unitOfWork.OrderHeader.Get(
            o => o.Id == orderId,
            includeProperties: "ApplicationUser",
            tracked: true);
        if (order is null) return;

        if (order.PaymentReceiptEmailSentUtc.HasValue)
        {
            _logger.LogInformation("Payment receipt already sent for order {OrderId} at {SentAt}, skipping.", orderId, order.PaymentReceiptEmailSentUtc);
            return;
        }

        var userEmail = order.ApplicationUser?.Email;
        if (string.IsNullOrWhiteSpace(userEmail)) return;

        var content = EmailTemplates.PaymentReceipt(
            _branding.PublicName,
            order.Name,
            order.Id,
            order.OrderTotal.ToString("C"),
            SD.SiteUrl,
            Thread.CurrentThread.CurrentUICulture);

        await TrySendEmailAsync(orderId, userEmail, content,
            () => order.PaymentReceiptEmailSentUtc = DateTime.UtcNow,
            "payment receipt");
    }

    public async Task TrySendPaymentFailedAsync(int orderId)
    {
        var order = _unitOfWork.OrderHeader.Get(
            o => o.Id == orderId,
            includeProperties: "ApplicationUser");
        if (order is null) return;

        var userEmail = order.ApplicationUser?.Email;
        if (string.IsNullOrWhiteSpace(userEmail)) return;

        var content = EmailTemplates.PaymentFailed(
            _branding.PublicName,
            order.Name,
            order.Id,
            SD.SiteUrl,
            Thread.CurrentThread.CurrentUICulture);

        await TrySendEmailAsync(orderId, userEmail, content,
            () => { },
            "payment failed");
    }

    public async Task TrySendShippingConfirmationAsync(int orderId)
    {
        var order = _unitOfWork.OrderHeader.Get(
            o => o.Id == orderId,
            includeProperties: "ApplicationUser",
            tracked: true);
        if (order is null) return;

        if (order.ShippingConfirmationEmailSentUtc.HasValue)
        {
            _logger.LogInformation("Shipping confirmation already sent for order {OrderId} at {SentAt}, skipping.", orderId, order.ShippingConfirmationEmailSentUtc);
            return;
        }

        var userEmail = order.ApplicationUser?.Email;
        if (string.IsNullOrWhiteSpace(userEmail)) return;

        var content = EmailTemplates.ShippingConfirmation(
            _branding.PublicName,
            order.Name,
            order.Id,
            order.TrackingNumber,
            order.Carrier,
            SD.SiteUrl,
            Thread.CurrentThread.CurrentUICulture);

        await TrySendEmailAsync(orderId, userEmail, content,
            () => order.ShippingConfirmationEmailSentUtc = DateTime.UtcNow,
            "shipping confirmation");
    }

    public async Task TrySendAdminNewOrderAlertAsync(int orderId)
    {
        if (string.IsNullOrWhiteSpace(_adminEmail))
        {
            _logger.LogInformation("Admin email not configured, skipping admin alert for order {OrderId}.", orderId);
            return;
        }

        var order = _unitOfWork.OrderHeader.Get(
            o => o.Id == orderId,
            includeProperties: "ApplicationUser");
        if (order is null) return;

        var content = EmailTemplates.AdminNewOrderAlert(
            _branding.PublicName,
            order.Id,
            order.Name,
            order.OrderTotal.ToString("C"),
            EmailTemplates.OrderDetailsUrl(SD.SiteUrl, Thread.CurrentThread.CurrentUICulture, order.Id),
            Thread.CurrentThread.CurrentUICulture,
            order.PaymentMethod,
            order.CompanyId.GetValueOrDefault() > 0 && order.PaymentStatus == SD.PaymentStatusDelayedPayment);

        await TrySendEmailAsync(orderId, _adminEmail, content,
            () => { },
            "admin new order alert");
    }

    public async Task TrySendAdminBankTransferConfirmationRequestAsync(int orderId)
    {
        if (string.IsNullOrWhiteSpace(_adminEmail))
        {
            _logger.LogInformation("Admin email not configured, skipping bank transfer confirmation alert for order {OrderId}.", orderId);
            return;
        }

        var order = _unitOfWork.OrderHeader.Get(
            o => o.Id == orderId,
            includeProperties: "ApplicationUser",
            tracked: true);
        if (order is null || !order.TransferConfirmedByCustomerAt.HasValue)
        {
            return;
        }

        if (order.AdminBankTransferAlertEmailSentUtc.HasValue)
        {
            _logger.LogInformation("Admin bank transfer confirmation alert already sent for order {OrderId} at {SentAt}, skipping.", orderId, order.AdminBankTransferAlertEmailSentUtc);
            return;
        }

        var content = EmailTemplates.AdminBankTransferConfirmationRequest(
            _branding.PublicName,
            order.Id,
            order.Name,
            order.OrderTotal.ToString("C"),
            EmailTemplates.OrderDetailsUrl(SD.SiteUrl, Thread.CurrentThread.CurrentUICulture, order.Id),
            Thread.CurrentThread.CurrentUICulture);

        await TrySendEmailAsync(orderId, _adminEmail, content,
            () => order.AdminBankTransferAlertEmailSentUtc = DateTime.UtcNow,
            "admin bank transfer confirmation alert");
    }

    private async Task TrySendEmailAsync(
        int orderId, string recipient, EmailContent content,
        Action onSuccess, string emailType)
    {
        try
        {
            await _emailSender.SendEmailAsync(recipient, content.Subject, content.Body);
            onSuccess();
            _unitOfWork.Save();
            _logger.LogInformation("Sent {EmailType} email for order {OrderId} to {Recipient}.", emailType, orderId, recipient);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send {EmailType} email for order {OrderId} to {Recipient}. Order state preserved.", emailType, orderId, recipient);
            // ponytail: email failure never rolls back the order; log and move on
        }
    }
}
