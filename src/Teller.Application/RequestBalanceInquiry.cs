using Teller.Domain;

namespace Teller.Application;

public sealed record BalanceInquiryCommand(
    Guid InquiryId,
    AccountNumber AccountNumber,
    string TellerId,
    string BranchId);

public sealed record BalanceInquiryOutcome(BalanceInquiry Inquiry, bool Duplicate, bool PayloadMismatch = false);

public sealed class RequestBalanceInquiry(IBalanceInquiryRepository repository, IBalanceInquiryClient client, TimeProvider clock)
{
    public async Task<BalanceInquiryOutcome> Handle(BalanceInquiryCommand command, CancellationToken cancellationToken = default)
    {
        var existing = repository.Find(command.InquiryId);
        if (existing is not null)
            return new BalanceInquiryOutcome(existing, Duplicate: true, PayloadMismatch: !Matches(existing, command));

        var inquiry = await client.Request(
            command.InquiryId,
            command.AccountNumber,
            command.TellerId,
            command.BranchId,
            clock.GetUtcNow(),
            cancellationToken);

        if (inquiry.Status != BalanceInquiryStatus.TimedOut)
            repository.Add(inquiry);

        return new BalanceInquiryOutcome(inquiry, Duplicate: false);
    }

    private static bool Matches(BalanceInquiry inquiry, BalanceInquiryCommand command) =>
        inquiry.AccountNumber == command.AccountNumber.Value
        && inquiry.TellerId == command.TellerId.Trim()
        && inquiry.BranchId == command.BranchId.Trim();
}
