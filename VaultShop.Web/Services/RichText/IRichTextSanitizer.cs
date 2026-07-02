namespace UkiyoDesignsWeb.Services.RichText
{
    public interface IRichTextSanitizer
    {
        string? Sanitize(string? html);
    }
}
