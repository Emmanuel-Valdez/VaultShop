using System;

namespace VaultShop.Utility
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

		public const string PaymentMethodStripe = "Stripe";
		public const string PaymentMethodBankTransfer = "BankTransfer";
		public const string PaymentMethodMercadoPago = "MercadoPago";

		public const string DeliveryTypePickup = "S";

		// ponytail: Correo Argentino province codes (A–Z minus I/Ñ/O, 24 total); display names mirror tools/refresh_sucursales.py fallback. Seed codes must stay ⊆ this list (see CorreoProvincesTests).
		public static readonly IReadOnlyList<(string Code, string Name)> CorreoProvinces =
		[
			("A", "Salta"), ("B", "Buenos Aires"), ("C", "CABA"), ("D", "San Luis"),
			("E", "Entre Ríos"), ("F", "La Rioja"), ("G", "Santiago del Estero"),
			("H", "Chaco"), ("J", "San Juan"), ("K", "Catamarca"), ("L", "La Pampa"),
			("M", "Mendoza"), ("N", "Misiones"), ("P", "Formosa"), ("Q", "Neuquén"),
			("R", "Río Negro"), ("S", "Santa Fe"), ("T", "Tucumán"),
			("U", "Chubut"), ("V", "Tierra del Fuego"), ("W", "Corrientes"),
			("X", "Córdoba"), ("Y", "Jujuy"), ("Z", "Santa Cruz"),
		];

		public const string SessionCart = "SessionShoppingCart";
		public const string AdminPreviewMode = "AdminPreviewMode";

		public const int CompanyPaymentDueDays = 5;

        public static string TikTokLink => GetEnvOrDefault("Social__TikTok", "");
        public static string WhatsAppLink => GetEnvOrDefault("Social__WhatsApp", "");
        public static string InstagramLink => GetEnvOrDefault("Social__Instagram", "");
        public static string FacebookLink => GetEnvOrDefault("Social__Facebook", "");
        public static string DevLink => GetEnvOrDefault("Social__DevLink", "");
        public static string SiteUrl => GetEnvOrDefault("SiteUrl", "https://vaultshop.evaldez.ar");

        private static string GetEnvOrDefault(string key, string defaultValue)
        {
            var value = Environment.GetEnvironmentVariable(key);
            return string.IsNullOrEmpty(value) ? defaultValue : value;
        }
    }
}