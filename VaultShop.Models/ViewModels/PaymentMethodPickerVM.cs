namespace VaultShop.Models.ViewModels
{
    public class PaymentMethodPickerVM
    {
        public string FieldName { get; set; } = "paymentMethod";
        public string IdPrefix { get; set; } = "paymentMethod";
        public string SelectedPaymentMethod { get; set; } = string.Empty;

        public bool StripeEnabled { get; set; }
        public bool BankTransferEnabled { get; set; }

        public string PayWithStripeLabel { get; set; } = "Stripe";
        public string PayByBankTransferLabel { get; set; } = "Bank transfer";
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