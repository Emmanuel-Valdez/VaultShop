using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

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
		public const string DeliveryTypeMotorcycle = "M";
		public const string DeliveryTypeVendorPickup = "V";

		// Frozen wording for vendor-arranged pickup orders (spec: delivery to be arranged at the Microcentro of Mendoza).
		public const string VendorPickupDeliveryNote = "Entrega a acordar con el vendedor en el Microcentro de Mendoza";

		// Gran Mendoza (26 localities). Zone membership only toggles delivery modality availability;
		// the shipping charge itself is zone-independent, so a forged locality cannot game pricing.
		public static readonly IReadOnlyList<string> GranMendozaLocalities =
		[
			"Bermejo", "Capilla del Rosario", "Carrodilla", "Chacras de Coria", "Cieneguita",
			"Dorrego", "El Challao", "El Resguardo", "General Belgrano", "Godoy Cruz",
			"Kilómetro 11", "Kilómetro 8", "Maipú", "Las Cañas", "Las Heras",
			"Las Tortugas", "Luján de Cuyo", "Luzuriaga", "Mendoza", "Rodeo de la Cruz",
			"Rodeo del Medio", "San Francisco del Monte", "San José", "Villa Hipódromo",
			"Villa Marini", "Villa Nueva",
		];

		// Precomputed so matching is O(1); catalog spellings vary in case/accents (LAS CANAS ≡ Las Cañas).
		private static readonly HashSet<string> GranMendozaNormalized =
			GranMendozaLocalities.Select(NormalizeLocality).ToHashSet(StringComparer.Ordinal);

		public static bool IsGranMendoza(string? locality)
		{
			var normalized = NormalizeLocality(locality);
			return normalized.Length > 0 && GranMendozaNormalized.Contains(normalized);
		}

		// Catalog locality matching: "LAS CANAS" ≡ "Las Cañas" (case- and accent-insensitive).
		public static bool LocalityEquals(string? left, string? right)
			=> NormalizeLocality(left) == NormalizeLocality(right);

		// Case- and accent-insensitive: strip diacritics (FormD), uppercase, trim.
		private static string NormalizeLocality(string? value)
		{
			if (string.IsNullOrWhiteSpace(value))
			{
				return string.Empty;
			}

			var normalized = new StringBuilder(value.Length);
			foreach (var ch in value.Normalize(NormalizationForm.FormD))
			{
				if (CharUnicodeInfo.GetUnicodeCategory(ch) != UnicodeCategory.NonSpacingMark)
				{
					normalized.Append(char.ToUpperInvariant(ch));
				}
			}

			return normalized.ToString().Trim();
		}

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
		public const string SessionCouponCode = "SessionCouponCode";

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