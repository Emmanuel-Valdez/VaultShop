namespace VaultShop.Models
{
    public class ErrorViewModel
    {
        public int? StatusCode { get; set; }

        public string? Title { get; set; }

        public string? Message { get; set; }

        public string? RequestId { get; set; }

        public int? RetryAfterSeconds { get; set; }

        public string? RetryPath { get; set; }

        public bool ShowRequestId => !string.IsNullOrEmpty(RequestId);
    }
}
