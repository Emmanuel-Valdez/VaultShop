using System.Text;
using System.Text.RegularExpressions;
using System.Globalization;

namespace VaultShop.Utility
{
    public static class SlugHelper
    {
        private static readonly Regex NonAlphanumeric = new("[^a-z0-9]+", RegexOptions.Compiled);

        public static string Slugify(string? input)
        {
            if (string.IsNullOrWhiteSpace(input))
            {
                return string.Empty;
            }

            var ascii = new StringBuilder(input.Length);
            foreach (var ch in input.Normalize(NormalizationForm.FormD))
            {
                if (ch < 128)
                {
                    ascii.Append(char.ToLowerInvariant(ch));
                }
            }

            return NonAlphanumeric.Replace(ascii.ToString(), "-").Trim('-');
        }
    }
}