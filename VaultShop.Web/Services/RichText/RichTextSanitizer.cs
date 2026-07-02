using Ganss.Xss;

namespace VaultShop.Web.Services.RichText
{
    public sealed class RichTextSanitizer : IRichTextSanitizer
    {
        private readonly HtmlSanitizer _sanitizer;

        public RichTextSanitizer()
        {
            _sanitizer = new HtmlSanitizer();

            _sanitizer.AllowedTags.Clear();
            foreach (var tag in new[] { "p", "br", "strong", "em", "u", "s", "ol", "ul", "li", "a", "h2", "h3" })
            {
                _sanitizer.AllowedTags.Add(tag);
            }

            _sanitizer.AllowedAttributes.Clear();
            foreach (var attribute in new[] { "href", "target", "rel" })
            {
                _sanitizer.AllowedAttributes.Add(attribute);
            }

            _sanitizer.AllowedSchemes.Clear();
            _sanitizer.AllowedSchemes.Add("http");
            _sanitizer.AllowedSchemes.Add("https");
            _sanitizer.AllowedSchemes.Add("mailto");
        }

        public string? Sanitize(string? html)
        {
            if (string.IsNullOrWhiteSpace(html))
            {
                return html;
            }

            var sanitized = _sanitizer.Sanitize(html).Trim();
            return sanitized == "<p><br></p>" ? string.Empty : sanitized;
        }
    }
}
