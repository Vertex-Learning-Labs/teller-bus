namespace MockHost.Infrastructure;

public sealed class MockHostOptions
{
    public const string SectionName = "MockHost";

    public int TimeoutAfterPostDelaySeconds { get; set; } = 15;

    public string? LedgerPath { get; set; }

    public List<AccountSeed> Accounts { get; set; } = [];

    public TimeSpan TimeoutAfterPostDelay => TimeSpan.FromSeconds(Math.Max(0, TimeoutAfterPostDelaySeconds));
}

public sealed class AccountSeed
{
    public string AccountNumber { get; set; } = "";

    public decimal AvailableBalance { get; set; }

    public string Currency { get; set; } = "USD";

    public string Status { get; set; } = "Open";
}
