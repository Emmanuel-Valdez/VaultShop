using VaultShop.Utility;

namespace VaultShop.Web.Tests;

public class SlugHelperTests
{
    [Theory]
    [InlineData("Naruto", "naruto")]
    [InlineData("Studio Ghibli", "studio-ghibli")]
    [InlineData("My Hero Academia", "my-hero-academia")]
    [InlineData("Atelier Albums & Obras", "atelier-albums-obras")]
    [InlineData("   Collage   Nostalgia   ", "collage-nostalgia")]
    [InlineData("Descansos !!! y ---- Comas,", "descansos-y-comas")]
    [InlineData("K-on!!", "k-on")]
    public void Slugify_RendersExpectedSlug(string input, string expected)
    {
        Assert.Equal(expected, SlugHelper.Slugify(input));
    }

    [Theory]
    [InlineData("Ñoño & Cía", "nono-cia")]
    [InlineData("Éxodo Día 3", "exodo-dia-3")]
    [InlineData("ÁÉÍÓÚáéíóú→", "aeiouaeiou")]
    public void Slugify_AccentedInput_AsciiFoldsDiacritics(string input, string expected)
    {
        Assert.Equal(expected, SlugHelper.Slugify(input));
    }

    [Fact]
    public void Slugify_NullOrWhitespace_ReturnsEmpty()
    {
        Assert.Equal(string.Empty, SlugHelper.Slugify(null));
        Assert.Equal(string.Empty, SlugHelper.Slugify("   "));
    }

    // ponytail: ß doesn't decompose via FormD — current behavior drops it.
    // Document ceiling: if German locale needed, map ß→ss before FormD.
    [Theory]
    [InlineData("Straße", "strae")]
    [InlineData("Über cool", "uber-cool")]
    public void Slugify_NonDecomposableChars_AreDropped(string input, string expected)
    {
        Assert.Equal(expected, SlugHelper.Slugify(input));
    }

    [Theory]
    [InlineData(null, "Remera Negra", "remera-negra")]
    [InlineData("", "Camperas de Invierno", "camperas-de-invierno")]
    [InlineData("   ", "Studio Ghibli", "studio-ghibli")]
    public void ResolveSlugOrDefault_BlankSlug_FallsBackToName(string? slug, string name, string expected)
    {
        var (resolved, errorKey) = SlugHelper.ResolveSlugOrDefault(slug, name);

        Assert.Equal(expected, resolved);
        Assert.Null(errorKey);
    }

    [Theory]
    [InlineData("Mi REMERA!! negra", "mi-remera-negra")]
    [InlineData("  Naruto  ", "naruto")]
    public void ResolveSlugOrDefault_ManualSlug_IsNormalized(string slug, string expected)
    {
        var (resolved, errorKey) = SlugHelper.ResolveSlugOrDefault(slug, "ignored name");

        Assert.Equal(expected, resolved);
        Assert.Null(errorKey);
    }

    // CJK-only names fold to nothing — callers surface ErrorKey and keep the row unsaved.
    [Theory]
    [InlineData("進撃の巨人", null)]
    [InlineData(null, "進撃の巨人")]
    [InlineData("---", "Naruto")]
    [InlineData("", "   ")]
    public void ResolveSlugOrDefault_NormalizesToEmpty_ReturnsErrorKey(string? slug, string? name)
    {
        var (resolved, errorKey) = SlugHelper.ResolveSlugOrDefault(slug, name);

        Assert.Equal(string.Empty, resolved);
        Assert.Equal("SlugRequired", errorKey);
    }
}