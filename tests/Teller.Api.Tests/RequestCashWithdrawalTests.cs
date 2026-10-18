using Teller.Api.Adapters;
using Teller.Application;
using Teller.Domain;

namespace Teller.Api.Tests;

public class RequestCashWithdrawalTests
{
    [Fact]
    public async Task A_retry_publishes_a_withdrawal_whose_first_publish_failed()
    {
        var repository = new InMemoryWithdrawalRepository();
        var publisher = new FlakyPublisher(failures: 1);
        var withdrawals = new RequestCashWithdrawal(repository, publisher, TimeProvider.System);
        var command = Command();

        await Assert.ThrowsAsync<InvalidOperationException>(() => withdrawals.Handle(command));
        Assert.False(repository.Find(command.TransactionId)!.Published);

        var retry = await withdrawals.Handle(command);

        Assert.True(retry.Duplicate);
        Assert.True(retry.Withdrawal.Published);
        Assert.Single(publisher.Messages);
    }

    [Fact]
    public async Task A_retry_after_a_successful_publish_does_not_publish_again()
    {
        var repository = new InMemoryWithdrawalRepository();
        var publisher = new FlakyPublisher(failures: 0);
        var withdrawals = new RequestCashWithdrawal(repository, publisher, TimeProvider.System);
        var command = Command();

        await withdrawals.Handle(command);
        var retry = await withdrawals.Handle(command);

        Assert.True(retry.Duplicate);
        Assert.Single(publisher.Messages);
    }

    [Fact]
    public async Task A_withdrawal_that_already_has_an_outcome_is_not_republished()
    {
        var repository = new InMemoryWithdrawalRepository();
        var publisher = new FlakyPublisher(failures: 1);
        var withdrawals = new RequestCashWithdrawal(repository, publisher, TimeProvider.System);
        var command = Command();

        await Assert.ThrowsAsync<InvalidOperationException>(() => withdrawals.Handle(command));
        new RecordWithdrawalOutcome(repository).Apply(command.TransactionId, RecordWithdrawalOutcome.Posted);
        await withdrawals.Handle(command);

        Assert.Empty(publisher.Messages);
    }

    [Fact]
    public async Task Reusing_a_key_with_a_different_teller_is_a_payload_mismatch()
    {
        var repository = new InMemoryWithdrawalRepository();
        var withdrawals = new RequestCashWithdrawal(repository, new FlakyPublisher(failures: 0), TimeProvider.System);
        var command = Command();

        await withdrawals.Handle(command);
        var reused = await withdrawals.Handle(command with { TellerId = "teller-2" });
        var trimmed = await withdrawals.Handle(command with { TellerId = "  teller-1 " });

        Assert.True(reused.PayloadMismatch);
        Assert.False(trimmed.PayloadMismatch);
    }

    private static CashWithdrawalCommand Command()
    {
        Assert.True(AccountNumber.TryCreate("1001001001", out var account));
        Assert.True(Money.TryCreate(40m, "USD", out var amount));
        return new CashWithdrawalCommand(Guid.NewGuid(), account, amount, "teller-1", "branch-9");
    }

    private sealed class FlakyPublisher(int failures) : IWithdrawalPublisher
    {
        private int _remaining = failures;

        public List<CashWithdrawalRequested> Messages { get; } = [];

        public Task Publish(CashWithdrawalRequested command, CancellationToken cancellationToken = default)
        {
            if (_remaining-- > 0)
                throw new InvalidOperationException("Broker unavailable.");

            Messages.Add(command);
            return Task.CompletedTask;
        }
    }
}
