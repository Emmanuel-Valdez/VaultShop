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

        /// <summary>
        /// Shared slug resolution for the admin upserts: a blank slug falls back to the
        /// entity name, then gets normalized. Uniqueness stays in the controllers (needs the DB).
        /// </summary>
        /// <returns>The resolved slug, plus an error key when normalization produced nothing.</returns>
        public static (string Slug, string? ErrorKey) ResolveSlugOrDefault(string? slug, string? name)
        {
            var resolved = Slugify(string.IsNullOrWhiteSpace(slug) ? name : slug);

            return string.IsNullOrEmpty(resolved)
                ? (string.Empty, "SlugRequired")
                : (resolved, null);
        }
    }
}