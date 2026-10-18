using Teller.Domain;

namespace Teller.Application;

public interface IWithdrawalRepository
{
    Withdrawal? Find(Guid transactionId);

    Withdrawal AddOrGet(Withdrawal withdrawal);
}

public interface IWithdrawalPublisher
{
    Task Publish(CashWithdrawalRequested command, CancellationToken cancellationToken = default);
}

public interface IBalanceInquiryRepository
{
    BalanceInquiry? Find(Guid inquiryId);

    void Add(BalanceInquiry inquiry);
}

public interface IBalanceInquiryClient
{
    Task<BalanceInquiry> Request(
        Guid inquiryId,
        AccountNumber accountNumber,
        string tellerId,
        string branchId,
        DateTimeOffset requestedAt,
        CancellationToken cancellationToken = default);
}
