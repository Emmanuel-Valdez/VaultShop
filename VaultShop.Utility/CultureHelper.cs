using System;
using System.Globalization;

namespace VaultShop.Utility
{
    /// <summary>
    /// Swaps the culture segment that leads a relative URL. Culture is the first segment of the
    /// route, so only that segment is touched and a culture-like token anywhere else survives
    /// untouched. Shared by the hreflang alternates and SetLanguage so the two cannot disagree.
    /// </summary>
    public static class CultureHelper
    {
        /// <summary>Recultures a root-relative URL by replacing its leading culture segment.</summary>
        /// <param name="relativeUrl">Root-relative URL, with or without a query string.</param>
        /// <param name="culture">Target culture name, e.g. <c>es-AR</c>.</param>
        /// <returns>
        /// The recultured URL. The path remainder and the query string are carried across
        /// byte-identical; a null, empty or root input becomes <c>/{culture}</c>.
        /// </returns>
        public static string SwapCultureSegment(string? relativeUrl, string culture)
        {
            var relative = relativeUrl ?? string.Empty;
            var queryAt = relative.IndexOf('?');
            var path = (queryAt < 0 ? relative : relative[..queryAt]).TrimStart('/');
            var query = queryAt < 0 ? string.Empty : relative[queryAt..];

            if (path.Length == 0)
            {
                return $"/{culture}{query}";
            }

            var slash = path.IndexOf('/');
            var head = slash < 0 ? path : path[..slash];
            var rest = slash < 0 ? string.Empty : path[slash..];

            // A URL built without the culture route still needs one, so prepend instead of
            // overwriting a route segment that is not a culture.
            return IsCulture(head) ? $"/{culture}{rest}{query}" : $"/{culture}/{path}{query}";
        }

        private static bool IsCulture(string segment)
        {
            try
            {
                return CultureInfo.GetCultureInfo(segment, predefinedOnly: true).Name.Length > 0;
            }
            catch (CultureNotFoundException)
            {
                return false;
            }
        }
    }
}
