namespace Teller.Domain;

public enum WithdrawalStatus
{
    Accepted,
    Processing,
    Posted,
    Declined,
    NeedsReview
}

public sealed class Withdrawal
{
    private Withdrawal(
        Guid transactionId,
        AccountNumber accountNumber,
        Money amount,
        string tellerId,
        string branchId,
        DateTimeOffset requestedAt)
    {
        TransactionId = transactionId;
        AccountNumber = accountNumber;
        Amount = amount;
        TellerId = tellerId;
        BranchId = branchId;
        RequestedAt = requestedAt;
        Status = WithdrawalStatus.Accepted;
    }

    public Guid TransactionId { get; }

    public AccountNumber AccountNumber { get; }

    public Money Amount { get; }

    public string TellerId { get; }

    public string BranchId { get; }

    public DateTimeOffset RequestedAt { get; }

    public WithdrawalStatus Status { get; private set; }

    /// <summary>Why the withdrawal was declined or needs review, in terms safe to show the teller.</summary>
    public string? Reason { get; private set; }

    public bool Published { get; private set; }

    public static Withdrawal Accept(
        Guid transactionId,
        AccountNumber accountNumber,
        Money amount,
        string tellerId,
        string branchId,
        DateTimeOffset requestedAt)
    {
        if (transactionId == Guid.Empty)
            throw new ArgumentException("Transaction id is required.", nameof(transactionId));

        if (string.IsNullOrWhiteSpace(tellerId))
            throw new ArgumentException("Teller id is required.", nameof(tellerId));

        if (string.IsNullOrWhiteSpace(branchId))
            throw new ArgumentException("Branch id is required.", nameof(branchId));

        return new Withdrawal(
            transactionId,
            accountNumber,
            amount,
            tellerId.Trim(),
            branchId.Trim(),
            requestedAt);
    }

    public bool Matches(AccountNumber accountNumber, Money amount, string tellerId, string branchId) =>
        AccountNumber == accountNumber
        && Amount == amount
        && TellerId == tellerId.Trim()
        && BranchId == branchId.Trim();

    public void MarkPublished() => Published = true;

    public void MarkProcessing() => MoveTo(WithdrawalStatus.Processing, WithdrawalStatus.Accepted);

    public void Post() => MoveTo(WithdrawalStatus.Posted, WithdrawalStatus.Processing);

    public void Decline(string? reason = null)
    {
        if (Status is not (WithdrawalStatus.Accepted or WithdrawalStatus.Processing))
            throw new InvalidOperationException($"Cannot decline a withdrawal that is {Status}.");

        Status = WithdrawalStatus.Declined;
        Reason = reason;
    }

    public void MarkNeedsReview(string? reason = null)
    {
        if (Status is WithdrawalStatus.Posted or WithdrawalStatus.Declined)
            throw new InvalidOperationException($"Cannot review a withdrawal that is {Status}.");

        Status = WithdrawalStatus.NeedsReview;
        Reason = reason;
    }

    private void MoveTo(WithdrawalStatus next, WithdrawalStatus required)
    {
        if (Status != required)
            throw new InvalidOperationException($"Cannot move a withdrawal from {Status} to {next}.");

        Status = next;
    }
}
