using System.ComponentModel.DataAnnotations;
using System.Globalization;

namespace UkiyoDesigns.Models.Validation
{
	public sealed class LocalizedStringLengthAttribute : StringLengthAttribute
	{
		private readonly string _englishMessage;
		private readonly string _spanishMessage;

		public LocalizedStringLengthAttribute(int maximumLength, int minimumLength, string englishMessage, string spanishMessage)
			: base(maximumLength)
		{
			MinimumLength = minimumLength;
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
