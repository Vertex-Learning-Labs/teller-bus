using MassTransit;
using Teller.Application;
using Teller.Domain;

namespace Teller.Api.Adapters;

public sealed class RabbitWithdrawalPublisher(IPublishEndpoint publish) : IWithdrawalPublisher
{
    public Task Publish(CashWithdrawalRequested command, CancellationToken cancellationToken = default) =>
        publish.Publish(command, cancellationToken);
}

public sealed class RabbitBalanceInquiryClient(IRequestClient<BalanceInquiryRequest> client) : IBalanceInquiryClient
{
    public async Task<BalanceInquiry> Request(
        Guid inquiryId,
        AccountNumber accountNumber,
        string tellerId,
        string branchId,
        DateTimeOffset requestedAt,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await client.GetResponse<BalanceInquiryCompleted>(
                new BalanceInquiryRequest(inquiryId, accountNumber.Value, tellerId.Trim(), branchId.Trim(), requestedAt),
                cancellationToken);

            var message = response.Message;
            if (!message.Found)
            {
                return new BalanceInquiry(
                    inquiryId,
                    accountNumber.Value,
                    tellerId.Trim(),
                    branchId.Trim(),
                    BalanceInquiryStatus.UnknownAccount,
                    null,
                    null,
                    requestedAt);
            }

            return new BalanceInquiry(
                inquiryId,
                accountNumber.Value,
                tellerId.Trim(),
                branchId.Trim(),
                BalanceInquiryStatus.Answered,
                message.AvailableBalance,
                message.Currency,
                requestedAt);
        }
        catch (Exception exception) when (exception is RequestTimeoutException or RequestFaultException)
        {
            return new BalanceInquiry(
                inquiryId,
                accountNumber.Value,
                tellerId.Trim(),
                branchId.Trim(),
                BalanceInquiryStatus.TimedOut,
                null,
                null,
                requestedAt);
        }
    }
}
