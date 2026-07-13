using System.Globalization;

namespace VaultShop.Web.Services.Email;

public static class EmailTemplates
{
    public static EmailContent OrderConfirmation(
        string storeName, string customerName, int orderId,
        IEnumerable<OrderItemLine> items, string orderTotal, string siteUrl,
        CultureInfo culture)
    {
        var isSpanish = culture.Name.StartsWith("es", StringComparison.OrdinalIgnoreCase);
        var subject = isSpanish
            ? $"Pedido #{orderId} confirmado � {storeName}"
            : $"Order #{orderId} confirmed � {storeName}";

        var itemsHtml = string.Join("", items.Select(i =>
            $"<tr><td style='padding:8px;border-bottom:1px solid #eee;'>{i.ProductName}</td>" +
            $"<td style='padding:8px;border-bottom:1px solid #eee;text-align:center;'>{i.Quantity}</td>" +
            $"<td style='padding:8px;border-bottom:1px solid #eee;text-align:right;'>{i.Price}</td></tr>"));

        var greeting = isSpanish ? $"�Gracias, {customerName}!" : $"Thanks, {customerName}!";
        var orderNumberText = isSpanish ? $"Pedido N� {orderId}" : $"Order #{orderId}";
        var totalLabel = isSpanish ? "Total" : "Total";
        var viewOrderText = isSpanish ? "Ver pedido" : "View order";

        var body = $@"
<!DOCTYPE html>
<html><body style='font-family:sans-serif;margin:0;padding:0;background:#f4f4f4;'>
<div style='max-width:600px;margin:20px auto;background:#fff;border-radius:8px;overflow:hidden;'>
<div style='background:#1a1a2e;color:#fff;padding:20px;text-align:center;'>
<h1 style='margin:0;'>{storeName}</h1>
</div>
<div style='padding:24px;'>
<h2>{greeting}</h2>
<p>{orderNumberText}</p>
<table width='100%' cellpadding='0' cellspacing='0' style='margin:16px 0;'>
<thead><tr style='background:#f8f8f8;'>
<th style='padding:8px;text-align:left;'>{Translate("Product", culture)}</th>
<th style='padding:8px;text-align:center;'>{Translate("Qty", culture)}</th>
<th style='padding:8px;text-align:right;'>{Translate("Price", culture)}</th>
</tr></thead>
<tbody>{itemsHtml}</tbody>
</table>
<hr style='border:none;border-top:1px solid #eee;'>
<p style='font-size:18px;'><strong>{totalLabel}:</strong> {orderTotal}</p>
<p><a href='{siteUrl}' style='display:inline-block;padding:10px 20px;background:#1a1a2e;color:#fff;text-decoration:none;border-radius:4px;'>{viewOrderText}</a></p>
</div></div></body></html>";
        return new EmailContent(subject, body);
    }

    public static EmailContent PaymentReceipt(
        string storeName, string customerName, int orderId, string orderTotal, string siteUrl,
        CultureInfo culture)
    {
        var isSpanish = culture.Name.StartsWith("es", StringComparison.OrdinalIgnoreCase);
        var subject = isSpanish
            ? $"Pago recibido para el pedido #{orderId} � {storeName}"
            : $"Payment received for order #{orderId} � {storeName}";
        var heading = isSpanish ? "�Pago confirmado!" : "Payment confirmed!";
        var message = isSpanish
            ? $"Hemos recibido tu pago de {orderTotal} para el pedido N� {orderId}."
            : $"We've received your payment of {orderTotal} for order #{orderId}.";
        var dashboardText = isSpanish ? "Ver mis pedidos" : "My orders";

        var body = HtmlTemplate(storeName, heading, message, $"{siteUrl}/customer/order", dashboardText, culture);
        return new EmailContent(subject, body);
    }

    public static EmailContent PaymentFailed(
        string storeName, string customerName, int orderId, string siteUrl,
        CultureInfo culture)
    {
        var isSpanish = culture.Name.StartsWith("es", StringComparison.OrdinalIgnoreCase);
        var subject = isSpanish
            ? $"Pago fallido para el pedido #{orderId} � {storeName}"
            : $"Payment failed for order #{orderId} � {storeName}";
        var heading = isSpanish ? "Pago no procesado" : "Payment not processed";
        var message = isSpanish
            ? $"El pago del pedido N� {orderId} no pudo procesarse. Pod�s intentar nuevamente desde tu panel."
            : $"The payment for order #{orderId} could not be processed. You can try again from your dashboard.";
        var dashboardText = isSpanish ? "Intentar de nuevo" : "Try again";

        var body = HtmlTemplate(storeName, heading, message, $"{siteUrl}/customer/order", dashboardText, culture);
        return new EmailContent(subject, body);
    }

    public static EmailContent ShippingConfirmation(
        string storeName, string customerName, int orderId,
        string? trackingNumber, string? carrier, string siteUrl,
        CultureInfo culture)
    {
        var isSpanish = culture.Name.StartsWith("es", StringComparison.OrdinalIgnoreCase);
        var subject = isSpanish
            ? $"Pedido #{orderId} enviado � {storeName}"
            : $"Order #{orderId} shipped � {storeName}";
        var heading = isSpanish ? "�Tu pedido est� en camino!" : "Your order is on its way!";
        var message = isSpanish
            ? $"El pedido N� {orderId} ha sido despachado."
            : $"Order #{orderId} has been shipped.";
        if (!string.IsNullOrEmpty(trackingNumber))
        {
            message += isSpanish
                ? $" C�digo de seguimiento: {trackingNumber}"
                : $" Tracking number: {trackingNumber}";
        }
        if (!string.IsNullOrEmpty(carrier))
        {
            message += isSpanish
                ? $" Transporte: {carrier}"
                : $" Carrier: {carrier}";
        }
        var dashboardText = isSpanish ? "Seguir pedido" : "Track order";
        var body = HtmlTemplate(storeName, heading, message, $"{siteUrl}/customer/order", dashboardText, culture);
        return new EmailContent(subject, body);
    }

    public static EmailContent AdminNewOrderAlert(
        string storeName, int orderId, string customerName, string orderTotal, string adminUrl,
        CultureInfo culture)
    {
        var isSpanish = culture.Name.StartsWith("es", StringComparison.OrdinalIgnoreCase);
        var subject = isSpanish
            ? $"[Admin] Nuevo pedido #{orderId} � {storeName}"
            : $"[Admin] New order #{orderId} � {storeName}";
        var heading = isSpanish ? "Nuevo pedido recibido" : "New order received";
        var message = isSpanish
            ? $"El cliente {customerName} realiz� el pedido N� {orderId} por {orderTotal}."
            : $"Customer {customerName} placed order #{orderId} for {orderTotal}.";
        var dashboardText = isSpanish ? "Ver pedido" : "View order";
        var body = HtmlTemplate(storeName, heading, message, adminUrl, dashboardText, culture);
        return new EmailContent(subject, body);
    }

    public static EmailContent AdminBankTransferConfirmationRequest(
        string storeName, int orderId, string customerName, string orderTotal, string adminUrl,
        CultureInfo culture)
    {
        var isSpanish = culture.Name.StartsWith("es", StringComparison.OrdinalIgnoreCase);
        var subject = isSpanish
            ? $"[Admin] Cliente reportó transferencia para pedido #{orderId} - {storeName}"
            : $"[Admin] Customer reported bank transfer for order #{orderId} - {storeName}";
        var heading = isSpanish ? "Transferencia reportada por el cliente" : "Customer reported bank transfer";
        var message = isSpanish
            ? $"{customerName} informó que ya transfirió el pago del pedido N° {orderId} por {orderTotal}. Revisá el ingreso y aprobá el pago cuando impacte."
            : $"{customerName} reported sending the bank transfer for order #{orderId} totaling {orderTotal}. Review the incoming payment and approve it once it arrives.";
        var dashboardText = isSpanish ? "Revisar pedido" : "Review order";
        var body = HtmlTemplate(storeName, heading, message, adminUrl, dashboardText, culture);
        return new EmailContent(subject, body);
    }

    private static string HtmlTemplate(
        string storeName, string heading, string message,
        string actionUrl, string actionText, CultureInfo culture)
    {
        return $@"
<!DOCTYPE html>
<html><body style='font-family:sans-serif;margin:0;padding:0;background:#f4f4f4;'>
<div style='max-width:600px;margin:20px auto;background:#fff;border-radius:8px;overflow:hidden;'>
<div style='background:#1a1a2e;color:#fff;padding:20px;text-align:center;'>
<h1 style='margin:0;'>{storeName}</h1>
</div>
<div style='padding:24px;'>
<h2>{heading}</h2>
<p>{message}</p>
<p><a href='{actionUrl}' style='display:inline-block;padding:10px 20px;background:#1a1a2e;color:#fff;text-decoration:none;border-radius:4px;'>{actionText}</a></p>
</div></div></body></html>";
    }

    private static string Translate(string key, CultureInfo culture)
    {
        // ponytail: inline translation for template table headers � 3 keys, not worth full resx
        var isSpanish = culture.Name.StartsWith("es", StringComparison.OrdinalIgnoreCase);
        return key switch
        {
            "Product" => isSpanish ? "Producto" : "Product",
            "Qty" => isSpanish ? "Cant." : "Qty",
            "Price" => isSpanish ? "Precio" : "Price",
            _ => key,
        };
    }
}

public sealed record OrderItemLine(string ProductName, int Quantity, string Price);
