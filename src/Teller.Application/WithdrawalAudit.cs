namespace Teller.Application;

public sealed record WithdrawalAuditEntry(Guid TransactionId, DateTimeOffset At, string Event, string? Detail);

/// <summary>
/// Events in a withdrawal's audit trail. Detail on these entries is for operations staff and may include
/// technical causes; only <see cref="Teller.Domain.Withdrawal.Reason"/> is shown to the teller.
/// </summary>
public static class WithdrawalAuditEvents
{
    public const string Accepted = "Accepted";
    public const string Repeated = "Repeated";
    public const string KeyReused = "KeyReused";
    public const string Published = "Published";
    public const string PublishFailed = "PublishFailed";
    public const string HostAnswered = "HostAnswered";
    public const string HostFaulted = "HostFaulted";
    public const string Posted = "Posted";
    public const string Declined = "Declined";
    public const string NeedsReview = "NeedsReview";
    public const string OutcomeIgnored = "OutcomeIgnored";
}
