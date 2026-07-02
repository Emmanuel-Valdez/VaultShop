using System.ComponentModel.DataAnnotations;
using System.Globalization;

namespace VaultShop.Models.Validation
{
	public sealed class LocalizedRangeAttribute : RangeAttribute
	{
		private readonly string _englishMessage;
		private readonly string _spanishMessage;

		public LocalizedRangeAttribute(double minimum, double maximum, string englishMessage, string spanishMessage)
			: base(minimum, maximum)
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
