using System.ComponentModel.DataAnnotations;
using System.Globalization;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;

namespace UkiyoDesigns.Models.Validation
{
	public sealed class LocalizedEmailAddressAttribute : ValidationAttribute, IClientModelValidator
	{
		private readonly EmailAddressAttribute _emailAddressAttribute = new();
		private readonly string _englishMessage;
		private readonly string _spanishMessage;

		public LocalizedEmailAddressAttribute(string englishMessage, string spanishMessage)
		{
			_englishMessage = englishMessage;
			_spanishMessage = spanishMessage;
		}

		public override bool IsValid(object? value)
		{
			return _emailAddressAttribute.IsValid(value);
		}

		public override string FormatErrorMessage(string name)
		{
			return CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "es"
				? _spanishMessage
				: _englishMessage;
		}

		public void AddValidation(ClientModelValidationContext context)
		{
			MergeAttribute(context.Attributes, "data-val", "true");
			MergeAttribute(context.Attributes, "data-val-email", FormatErrorMessage(context.ModelMetadata.GetDisplayName()));
		}

		private static bool MergeAttribute(IDictionary<string, string> attributes, string key, string value)
		{
			if (attributes.ContainsKey(key))
			{
				return false;
			}

			attributes.Add(key, value);
			return true;
		}
	}
}
