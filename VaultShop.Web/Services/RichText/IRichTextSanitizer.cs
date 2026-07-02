namespace VaultShop.Web.Services.RichText
{
    public interface IRichTextSanitizer
    {
        string? Sanitize(string? html);
    }
}
