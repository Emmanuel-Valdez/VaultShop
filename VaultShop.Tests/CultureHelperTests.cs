using VaultShop.Utility;

namespace VaultShop.Web.Tests;

// seo-canonical-hreflang 1.1 — culture is the first route segment, so only that segment moves.
public class CultureHelperTests
{
    [Theory]
    [InlineData("/es-AR/Customer/Home/Index", "en-US", "/en-US/Customer/Home/Index")]
    [InlineData("/en-US/Customer/Home/Details/12/remera-negra", "es-AR", "/es-AR/Customer/Home/Details/12/remera-negra")]
    [InlineData("/es-AR", "en-US", "/en-US")]
    [InlineData("/es-AR/Customer/Home/Search?categoryId=3&cslug=mochilas", "en-US", "/en-US/Customer/Home/Search?categoryId=3&cslug=mochilas")]
    public void SwapCultureSegment_ReplacesTheLeadingSegmentOnly(string relativeUrl, string culture, string expected)
    {
        var swapped = CultureHelper.SwapCultureSegment(relativeUrl, culture);

        Assert.Equal(expected, swapped);
        // a doubled segment is the defect this helper exists to prevent
        Assert.Equal(1, swapped.Split('/').Count(s => s == culture));
    }

    [Theory]
    [InlineData("/Customer/Home/Index", "/en-US/Customer/Home/Index")]
    [InlineData("/Customer/Home/Index?categoryId=3", "/en-US/Customer/Home/Index?categoryId=3")]
    public void SwapCultureSegment_MissingCultureSegment_PrependsIt(string relativeUrl, string expected)
    {
        Assert.Equal(expected, CultureHelper.SwapCultureSegment(relativeUrl, "en-US"));
    }

    [Theory]
    [InlineData("/es-AR/Customer/Home/Search?searchString=es-AR", "/en-US/Customer/Home/Search?searchString=es-AR")]
    [InlineData("/es-AR/Customer/Home/Search?searchString=es-AR&cslug=ojos", "/en-US/Customer/Home/Search?searchString=es-AR&cslug=ojos")]
    [InlineData("/es-AR/Customer/Home/Details/9/es-AR", "/en-US/Customer/Home/Details/9/es-AR")]
    public void SwapCultureSegment_CultureTokenElsewhere_SurvivesUnchanged(string relativeUrl, string expected)
    {
        Assert.Equal(expected, CultureHelper.SwapCultureSegment(relativeUrl, "en-US"));
    }

    [Theory]
    [InlineData(null, "/en-US")]
    [InlineData("", "/en-US")]
    [InlineData("/", "/en-US")]
    [InlineData("/?categoryId=3", "/en-US?categoryId=3")]
    public void SwapCultureSegment_EmptyOrRootInput_YieldsCultureRoot(string? relativeUrl, string expected)
    {
        Assert.Equal(expected, CultureHelper.SwapCultureSegment(relativeUrl, "en-US"));
    }
}
