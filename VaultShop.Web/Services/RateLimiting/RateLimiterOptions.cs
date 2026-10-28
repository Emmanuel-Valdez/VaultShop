namespace VaultShop.Web.Services.RateLimiting;

public sealed class RateLimiterOptions
{
    public int GlobalPermitLimit { get; set; } = 300;
    public int GlobalWindowSeconds { get; set; } = 60;
    public int GlobalQueueLimit { get; set; } = 0;

    public int LoginPermitLimit { get; set; } = 10;
    public int LoginWindowSeconds { get; set; } = 60;
    public int LoginQueueLimit { get; set; } = 0;

    public int CheckoutPermitLimit { get; set; } = 10;
    public int CheckoutWindowSeconds { get; set; } = 60;
    public int CheckoutQueueLimit { get; set; } = 0;

    public int RecoveryPermitLimit { get; set; } = 5;
    public int RecoveryWindowSeconds { get; set; } = 60;
    public int RecoveryQueueLimit { get; set; } = 0;

    public int CartPermitLimit { get; set; } = 60;
    public int CartWindowSeconds { get; set; } = 60;
    public int CartQueueLimit { get; set; } = 0;

    public int SearchPermitLimit { get; set; } = 60;
    public int SearchWindowSeconds { get; set; } = 60;
    public int SearchQueueLimit { get; set; } = 0;

    public TimeSpan GlobalWindow => TimeSpan.FromSeconds(GlobalWindowSeconds);
    public TimeSpan LoginWindow => TimeSpan.FromSeconds(LoginWindowSeconds);
    public TimeSpan CheckoutWindow => TimeSpan.FromSeconds(CheckoutWindowSeconds);
    public TimeSpan RecoveryWindow => TimeSpan.FromSeconds(RecoveryWindowSeconds);
    public TimeSpan CartWindow => TimeSpan.FromSeconds(CartWindowSeconds);
    public TimeSpan SearchWindow => TimeSpan.FromSeconds(SearchWindowSeconds);
}
