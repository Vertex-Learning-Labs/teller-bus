using Teller.Domain;

namespace Teller.Application;

public sealed class RecordWithdrawalOutcome(IWithdrawalRepository repository)
{
    public const string Posted = "Posted";
    public const string Declined = "Declined";
    public const string NeedsReview = "NeedsReview";

    public void Apply(Guid transactionId, string outcome)
    {
        var withdrawal = repository.Find(transactionId);
        if (withdrawal is null)
            return;

        if (outcome == Posted)
        {
            if (withdrawal.Status == WithdrawalStatus.Accepted)
                withdrawal.MarkProcessing();

            if (withdrawal.Status == WithdrawalStatus.Processing)
                withdrawal.Post();

            return;
        }

        if (outcome == Declined)
        {
            if (withdrawal.Status is WithdrawalStatus.Accepted or WithdrawalStatus.Processing)
                withdrawal.Decline();

            return;
        }

        if (withdrawal.Status is not (WithdrawalStatus.Posted or WithdrawalStatus.Declined or WithdrawalStatus.NeedsReview))
            withdrawal.MarkNeedsReview();
    }
}
