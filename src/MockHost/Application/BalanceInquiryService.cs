using MockHost.Domain;

namespace MockHost.Application;

public sealed class BalanceInquiryService(IHostLedger ledger)
{
    public BalanceSnapshot? Get(AccountNumber accountNumber) => ledger.FindBalance(accountNumber);
}
