namespace VaultShop.Models.ViewModels
{
    public class PaymentMethodPickerVM
    {
        public string FieldName { get; set; } = "paymentMethod";
        public string IdPrefix { get; set; } = "paymentMethod";
        public string SelectedPaymentMethod { get; set; } = string.Empty;
        public string PaymentMethodLegend { get; set; } = "Payment method";

        public bool StripeEnabled { get; set; }
        public bool BankTransferEnabled { get; set; }
        public bool MercadoPagoEnabled { get; set; }

        public string PayWithStripeLabel { get; set; } = "Stripe";
        public string PayByBankTransferLabel { get; set; } = "Bank transfer";
        public string PayWithMercadoPagoLabel { get; set; } = "Mercado Pago";
        public string BankTransferCbuLabel { get; set; } = "CBU";
        public string BankTransferAliasLabel { get; set; } = "Alias";
        public string BankTransferRecipientNameLabel { get; set; } = "Recipient";
        public string BankTransferBankNameLabel { get; set; } = "Bank";

        public string BankTransferCbu { get; set; } = string.Empty;
        public string BankTransferAlias { get; set; } = string.Empty;
        public string BankTransferRecipientName { get; set; } = string.Empty;
        public string BankTransferBankName { get; set; } = string.Empty;
    }
}
