using Teller.Application;
using Teller.Domain;

namespace Teller.Api.Adapters;

public sealed class InMemoryWithdrawalRepository : IWithdrawalRepository
{
    private readonly object _gate = new();
    private readonly Dictionary<Guid, Withdrawal> _withdrawals = [];

    public Withdrawal? Find(Guid transactionId)
    {
        lock (_gate)
            return _withdrawals.GetValueOrDefault(transactionId);
    }

    public Withdrawal AddOrGet(Withdrawal withdrawal)
    {
        lock (_gate)
        {
            if (_withdrawals.TryGetValue(withdrawal.TransactionId, out var existing))
                return existing;

            _withdrawals.Add(withdrawal.TransactionId, withdrawal);
            return withdrawal;
        }
    }
}

public sealed class InMemoryBalanceInquiryRepository : IBalanceInquiryRepository
{
    private readonly object _gate = new();
    private readonly Dictionary<Guid, BalanceInquiry> _inquiries = [];

    public BalanceInquiry? Find(Guid inquiryId)
    {
        lock (_gate)
            return _inquiries.GetValueOrDefault(inquiryId);
    }

    public void Add(BalanceInquiry inquiry)
    {
        lock (_gate)
            _inquiries.TryAdd(inquiry.InquiryId, inquiry);
    }
}

public sealed class RecordingWithdrawalPublisher : IWithdrawalPublisher
{
    private readonly object _gate = new();
    private readonly List<CashWithdrawalRequested> _messages = [];

    public IReadOnlyList<CashWithdrawalRequested> Messages
    {
        get
        {
            lock (_gate)
                return _messages.ToArray();
        }
    }

    public Task Publish(CashWithdrawalRequested command, CancellationToken cancellationToken = default)
    {
        lock (_gate)
            _messages.Add(command);

        return Task.CompletedTask;
    }
}

public sealed class NotConnectedBalanceInquiryClient : IBalanceInquiryClient
{
    public Task<BalanceInquiry> Request(
        Guid inquiryId,
        AccountNumber accountNumber,
        string tellerId,
        string branchId,
        DateTimeOffset requestedAt,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(Create(inquiryId, accountNumber, tellerId, branchId, requestedAt));

    private static BalanceInquiry Create(
        Guid inquiryId,
        AccountNumber accountNumber,
        string tellerId,
        string branchId,
        DateTimeOffset requestedAt) =>
        new(
            inquiryId,
            accountNumber.Value,
            tellerId.Trim(),
            branchId.Trim(),
            BalanceInquiryStatus.Pending,
            AvailableBalance: null,
            Currency: null,
            requestedAt);
}
