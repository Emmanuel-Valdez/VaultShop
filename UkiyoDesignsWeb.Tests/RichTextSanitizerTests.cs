using UkiyoDesignsWeb.Services.RichText;

namespace UkiyoDesignsWeb.Tests;

public class RichTextSanitizerTests
{
    [Fact]
    public void Sanitize_DangerousHtml_RemovesScriptsEventHandlersAndImages()
    {
        var sanitizer = new RichTextSanitizer();
        const string html = "<p onclick=\"alert(1)\">Hello<script>alert(2)</script><img src=x onerror=\"alert(3)\"></p>";

        var result = sanitizer.Sanitize(html);

        Assert.Equal("<p>Hello</p>", result);
    }

    [Fact]
    public void Sanitize_AllowedEditorHtml_PreservesBasicFormattingAndSafeLinks()
    {
        var sanitizer = new RichTextSanitizer();
        const string html = "<h2>Title</h2><p><strong>Bold</strong> and <em>italic</em> <a href=\"https://example.com\" target=\"_blank\" rel=\"noopener\">link</a></p><ul><li>Item</li></ul>";

        var result = sanitizer.Sanitize(html);

        Assert.Equal(html, result);
    }

    [Fact]
    public void Sanitize_UnsafeLinkScheme_RemovesHref()
    {
        var sanitizer = new RichTextSanitizer();
        const string html = "<p><a href=\"javascript:alert(1)\">bad link</a></p>";

        var result = sanitizer.Sanitize(html);

        Assert.Equal("<p><a>bad link</a></p>", result);
    }
}
