using System;

namespace UkiyoDesigns.Utility
{
    public static class SD
    {
        public const string Role_Customer = "Customer";
        public const string Role_Company = "Company";
        public const string Role_Admin = "Admin";
        public const string Role_Employee = "Employee";

        public const string StatusPending="Pending";
		public const string StatusApproved = "Approved";
		public const string StatusInProcess = "Processing";
		public const string StatusShipped = "Shipped";
		public const string StatusCancelled = "Cancelled";
		public const string StatusRefunded = "Refunded";

		public const string PaymentStatusPending = "Pending";
		public const string PaymentStatusApproved = "Approved";
		public const string PaymentStatusDelayedPayment = "ApprovedForDelayedPayment";
		public const string PaymentStatusRejected = "Rejected";

		public const string SessionCart = "SessionShoppingCart";

        public static string TikTokLink => GetEnvOrDefault("Social__TikTok", "");
        public static string WhatsAppLink => GetEnvOrDefault("Social__WhatsApp", "");
        public static string InstagramLink => GetEnvOrDefault("Social__Instagram", "");
        public static string FacebookLink => GetEnvOrDefault("Social__Facebook", "");
        public static string EvalmonLink => GetEnvOrDefault("Social__Evalmon", "");
        public static string SiteUrl => GetEnvOrDefault("SiteUrl", "https://ukiyo.bsite.net");

        private static string GetEnvOrDefault(string key, string defaultValue)
        {
            var value = Environment.GetEnvironmentVariable(key);
            return string.IsNullOrEmpty(value) ? defaultValue : value;
        }
    }
}
