namespace VaultShop.Web.Services.Payments;

public sealed class PaymentReconciliationOptions
{
    public const string SectionName = "Payments:Reconciliation";

    public bool Enabled { get; set; } = false;

    public TimeSpan Interval { get; set; } = TimeSpan.FromMinutes(10);

    public TimeSpan StaleAfter { get; set; } = TimeSpan.FromMinutes(5);

    public TimeSpan MaxAge { get; set; } = TimeSpan.FromHours(48);

    public int BatchSize { get; set; } = 20;
}
