using Teller.Domain;

namespace Teller.Api.Tests;

public class WithdrawalTests
{
    [Fact]
    public void Accept_starts_at_accepted()
    {
        var withdrawal = Sample();

        Assert.Equal(WithdrawalStatus.Accepted, withdrawal.Status);
    }

    [Fact]
    public void Post_requires_processing_first()
    {
        var withdrawal = Sample();

        Assert.Throws<InvalidOperationException>(withdrawal.Post);
        withdrawal.MarkProcessing();
        withdrawal.Post();

        Assert.Equal(WithdrawalStatus.Posted, withdrawal.Status);
    }

    [Fact]
    public void Decline_is_allowed_before_the_host_posts()
    {
        var withdrawal = Sample();

        withdrawal.Decline();

        Assert.Equal(WithdrawalStatus.Declined, withdrawal.Status);
        Assert.Throws<InvalidOperationException>(withdrawal.MarkNeedsReview);
    }

    private static Withdrawal Sample()
    {
        Assert.True(AccountNumber.TryCreate("1001001001", out var account));
        Assert.True(Money.TryCreate(100m, "usd", out var amount));
        return Withdrawal.Accept(Guid.NewGuid(), account, amount, "teller-1", "branch-9", DateTimeOffset.UtcNow);
    }
}
