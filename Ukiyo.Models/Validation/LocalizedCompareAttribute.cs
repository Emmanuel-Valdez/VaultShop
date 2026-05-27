using System.ComponentModel.DataAnnotations;
using System.Globalization;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;

namespace UkiyoDesigns.Models.Validation
{
	public sealed class LocalizedCompareAttribute : CompareAttribute, IClientModelValidator
	{
		private readonly string _englishMessage;
		private readonly string _spanishMessage;

		public LocalizedCompareAttribute(string otherProperty, string englishMessage, string spanishMessage)
			: base(otherProperty)
		{
			_englishMessage = englishMessage;
			_spanishMessage = spanishMessage;
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
			MergeAttribute(context.Attributes, "data-val-equalto", FormatErrorMessage(context.ModelMetadata.GetDisplayName()));
			MergeAttribute(context.Attributes, "data-val-equalto-other", "*." + OtherProperty);
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
