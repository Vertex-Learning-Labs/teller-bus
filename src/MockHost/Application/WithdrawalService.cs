using MockHost.Domain;

namespace MockHost.Application;

public sealed class WithdrawalService(IHostLedger ledger)
{
    public WithdrawalReceipt? Post(Guid transactionId, AccountNumber accountNumber, Money amount) =>
        ledger.TryWithdraw(accountNumber, transactionId, amount);
}
