using Teller.Domain;

namespace Teller.Application;

public sealed class RecordWithdrawalOutcome(IWithdrawalRepository repository, IWithdrawalAuditLog audit)
{
    public const string Posted = "Posted";
    public const string Declined = "Declined";
    public const string NeedsReview = "NeedsReview";

    // Teller-facing reasons for when the host's answer is missing or unreadable. The technical cause goes to the
    // audit trail only, because exception text is not something a teller can act on.
    public const string HostOutcomeUnknown = "HostOutcomeUnknown";
    public const string UnrecognizedHostOutcome = "UnrecognizedHostOutcome";

    public void Apply(Guid transactionId, string outcome, string? reason = null)
    {
        var withdrawal = repository.Find(transactionId);
        if (withdrawal is null)
            return;

        var before = withdrawal.Status;

        if (outcome == Posted)
        {
            if (withdrawal.Status == WithdrawalStatus.Accepted)
                withdrawal.MarkProcessing();

            if (withdrawal.Status == WithdrawalStatus.Processing)
                withdrawal.Post();
        }
        else if (outcome == Declined)
        {
            if (withdrawal.Status is WithdrawalStatus.Accepted or WithdrawalStatus.Processing)
                withdrawal.Decline(reason);
        }
        else if (withdrawal.Status is not (WithdrawalStatus.Posted or WithdrawalStatus.Declined or WithdrawalStatus.NeedsReview))
        {
            withdrawal.MarkNeedsReview(outcome == NeedsReview ? reason ?? HostOutcomeUnknown : UnrecognizedHostOutcome);
        }

        if (withdrawal.Status == before)
            audit.Record(transactionId, WithdrawalAuditEvents.OutcomeIgnored, $"{outcome} arrived while the withdrawal was {before}.");
        else
            audit.Record(transactionId, withdrawal.Status.ToString(), withdrawal.Reason);
    }
}
