using Teller.Api.Adapters;
using Teller.Application;
using Teller.Domain;

namespace Teller.Api.Tests;

public class WithdrawalOutcomeTests
{
    [Fact]
    public void Posted_moves_an_accepted_withdrawal_to_posted()
    {
        var repository = new InMemoryWithdrawalRepository();
        var withdrawal = Accepted();
        repository.AddOrGet(withdrawal);

        new RecordWithdrawalOutcome(repository).Apply(withdrawal.TransactionId, RecordWithdrawalOutcome.Posted);
        new RecordWithdrawalOutcome(repository).Apply(withdrawal.TransactionId, RecordWithdrawalOutcome.Posted);

        Assert.Equal(WithdrawalStatus.Posted, withdrawal.Status);
    }

    [Fact]
    public void Declined_does_not_change_a_posted_withdrawal()
    {
        var repository = new InMemoryWithdrawalRepository();
        var withdrawal = Accepted();
        repository.AddOrGet(withdrawal);
        var outcomes = new RecordWithdrawalOutcome(repository);

        outcomes.Apply(withdrawal.TransactionId, RecordWithdrawalOutcome.Posted);
        outcomes.Apply(withdrawal.TransactionId, RecordWithdrawalOutcome.Declined);

        Assert.Equal(WithdrawalStatus.Posted, withdrawal.Status);
    }

    private static Withdrawal Accepted()
    {
        Assert.True(AccountNumber.TryCreate("1001001001", out var account));
        Assert.True(Money.TryCreate(40m, "USD", out var amount));
        return Withdrawal.Accept(Guid.NewGuid(), account, amount, "teller-1", "branch-9", DateTimeOffset.UtcNow);
    }
}
