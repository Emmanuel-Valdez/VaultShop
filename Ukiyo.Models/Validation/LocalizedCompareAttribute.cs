using System.ComponentModel.DataAnnotations;
using System.Globalization;

namespace UkiyoDesigns.Models.Validation
{
	public sealed class LocalizedCompareAttribute : CompareAttribute
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
	}
}
