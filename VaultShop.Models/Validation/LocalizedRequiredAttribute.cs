using System.ComponentModel.DataAnnotations;
using System.Globalization;

namespace VaultShop.Models.Validation
{
	public sealed class LocalizedRequiredAttribute : RequiredAttribute
	{
		private readonly string _englishMessage;
		private readonly string _spanishMessage;

		public LocalizedRequiredAttribute(string englishMessage, string spanishMessage)
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
	}
}
