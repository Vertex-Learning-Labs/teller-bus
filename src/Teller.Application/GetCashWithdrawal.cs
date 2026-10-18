using Teller.Domain;

namespace Teller.Application;

public sealed class GetCashWithdrawal(IWithdrawalRepository repository)
{
    public Withdrawal? Handle(Guid transactionId) => repository.Find(transactionId);
}
