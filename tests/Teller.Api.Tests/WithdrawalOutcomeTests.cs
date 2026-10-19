using Teller.Api.Adapters;
using Teller.Application;
using Teller.Domain;

namespace Teller.Api.Tests;

public class WithdrawalOutcomeTests
{
    private readonly InMemoryWithdrawalRepository _repository = new();
    private readonly InMemoryWithdrawalAuditLog _audit = new(TimeProvider.System);

    [Fact]
    public void Posted_moves_an_accepted_withdrawal_to_posted()
    {
        var withdrawal = Accepted();

        Outcomes().Apply(withdrawal.TransactionId, RecordWithdrawalOutcome.Posted);
        Outcomes().Apply(withdrawal.TransactionId, RecordWithdrawalOutcome.Posted);

        Assert.Equal(WithdrawalStatus.Posted, withdrawal.Status);
        Assert.Null(withdrawal.Reason);
    }

    [Fact]
    public void Declined_does_not_change_a_posted_withdrawal()
    {
        var withdrawal = Accepted();
        var outcomes = Outcomes();

        outcomes.Apply(withdrawal.TransactionId, RecordWithdrawalOutcome.Posted);
        outcomes.Apply(withdrawal.TransactionId, RecordWithdrawalOutcome.Declined, "InsufficientFunds");

        Assert.Equal(WithdrawalStatus.Posted, withdrawal.Status);
        Assert.Null(withdrawal.Reason);
    }

    [Fact]
    public void A_decline_keeps_the_host_reason_for_the_teller()
    {
        var withdrawal = Accepted();

        Outcomes().Apply(withdrawal.TransactionId, RecordWithdrawalOutcome.Declined, "InsufficientFunds");

        Assert.Equal(WithdrawalStatus.Declined, withdrawal.Status);
        Assert.Equal("InsufficientFunds", withdrawal.Reason);
    }

    [Fact]
    public void An_unknown_host_outcome_needs_review_with_a_generic_reason()
    {
        var withdrawal = Accepted();

        Outcomes().Apply(withdrawal.TransactionId, "Pending", "core said: queue full at node 7");

        Assert.Equal(WithdrawalStatus.NeedsReview, withdrawal.Status);
        Assert.Equal(RecordWithdrawalOutcome.UnrecognizedHostOutcome, withdrawal.Reason);
    }

    [Fact]
    public void Needs_review_without_a_reason_tells_the_teller_the_outcome_is_unknown()
    {
        var withdrawal = Accepted();

        Outcomes().Apply(withdrawal.TransactionId, RecordWithdrawalOutcome.NeedsReview);

        Assert.Equal(RecordWithdrawalOutcome.HostOutcomeUnknown, withdrawal.Reason);
    }

    [Fact]
    public void Each_outcome_is_audited_including_one_that_arrives_too_late()
    {
        var withdrawal = Accepted();
        var outcomes = Outcomes();

        outcomes.Apply(withdrawal.TransactionId, RecordWithdrawalOutcome.Declined, "AccountClosed");
        outcomes.Apply(withdrawal.TransactionId, RecordWithdrawalOutcome.Posted);

        var trail = _audit.For(withdrawal.TransactionId);
        Assert.Equal([WithdrawalAuditEvents.Declined, WithdrawalAuditEvents.OutcomeIgnored], trail.Select(entry => entry.Event));
        Assert.Equal("AccountClosed", trail[0].Detail);
        Assert.Equal("Posted arrived while the withdrawal was Declined.", trail[1].Detail);
    }

    private RecordWithdrawalOutcome Outcomes() => new(_repository, _audit);

    private Withdrawal Accepted()
    {
        Assert.True(AccountNumber.TryCreate("1001001001", out var account));
        Assert.True(Money.TryCreate(40m, "USD", out var amount));
        var withdrawal = Withdrawal.Accept(Guid.NewGuid(), account, amount, "teller-1", "branch-9", DateTimeOffset.UtcNow);
        _repository.AddOrGet(withdrawal);
        return withdrawal;
    }
}
