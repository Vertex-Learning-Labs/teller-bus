using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using MockHost.Application;
using MockHost.Domain;

namespace MockHost.Infrastructure;

public sealed class HostLedger : IHostLedger
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly object _gate = new();
    private readonly string? _path;
    private readonly Dictionary<string, Account> _accounts = new(StringComparer.Ordinal);
    private readonly Dictionary<Guid, WithdrawalReceipt> _receipts = [];

    public HostLedger(IOptions<MockHostOptions> options, IHostEnvironment? environment = null)
    {
        _path = ResolvePath(options.Value.LedgerPath, environment?.ContentRootPath);
        if (_path is not null && File.Exists(_path))
        {
            Load(_path);
            return;
        }

        Seed(options.Value.Accounts);
        Save();
    }

    public BalanceSnapshot? FindBalance(AccountNumber number)
    {
        lock (_gate)
        {
            return _accounts.TryGetValue(number.Value, out var account) ? ToBalance(account) : null;
        }
    }

    public WithdrawalReceipt? TryWithdraw(AccountNumber number, Guid transactionId, Money amount)
    {
        lock (_gate)
        {
            if (_receipts.TryGetValue(transactionId, out var existing))
                return existing with { Duplicate = true };

            if (!_accounts.TryGetValue(number.Value, out var account))
                return null;

            var ledger = account.Withdraw(amount);
            var receipt = new WithdrawalReceipt(
                transactionId,
                account.Number.Value,
                ledger.Outcome,
                ledger.Reason,
                ledger.Available.Amount,
                ledger.Available.Currency,
                Duplicate: false);

            _receipts.Add(transactionId, receipt);
            Save();
            return receipt;
        }
    }

    private void Seed(IEnumerable<AccountSeed> seeds)
    {
        foreach (var seed in seeds)
        {
            if (!AccountNumber.TryCreate(seed.AccountNumber, out var number))
                throw new InvalidOperationException($"Seed account '{seed.AccountNumber}' is not 6 to 20 digits.");

            if (!Money.TryCreate(seed.AvailableBalance, seed.Currency, out var available))
                throw new InvalidOperationException($"Seed account '{seed.AccountNumber}' has an invalid balance.");

            if (!Enum.TryParse<AccountStatus>(seed.Status, ignoreCase: true, out var status))
                throw new InvalidOperationException($"Seed account '{seed.AccountNumber}' has status '{seed.Status}'.");

            _accounts.Add(number.Value, Account.Restore(number, available, status));
        }
    }

    private void Load(string path)
    {
        var json = File.ReadAllText(path);
        var file = JsonSerializer.Deserialize<LedgerFile>(json, JsonOptions)
            ?? throw new InvalidOperationException($"Ledger file '{path}' is empty.");

        Seed(file.Accounts);
        foreach (var receipt in file.Receipts)
            _receipts.Add(receipt.TransactionId, receipt with { Duplicate = false });
    }

    private void Save()
    {
        if (_path is null)
            return;

        var file = new LedgerFile
        {
            Accounts = _accounts.Values.Select(account => new AccountSeed
            {
                AccountNumber = account.Number.Value,
                AvailableBalance = account.Available.Amount,
                Currency = account.Available.Currency,
                Status = account.Status.ToString()
            }).ToList(),
            Receipts = _receipts.Values.Select(receipt => receipt with { Duplicate = false }).ToList()
        };

        Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
        var temporary = _path + ".tmp";
        File.WriteAllText(temporary, JsonSerializer.Serialize(file, JsonOptions));
        File.Move(temporary, _path, overwrite: true);
    }

    private static BalanceSnapshot ToBalance(Account account) =>
        new(account.Number.Value, account.Available.Amount, account.Available.Currency, account.Status);

    private static string? ResolvePath(string? path, string? contentRoot)
    {
        if (string.IsNullOrWhiteSpace(path))
            return null;

        if (Path.IsPathRooted(path))
            return path;

        return Path.Combine(contentRoot ?? Directory.GetCurrentDirectory(), path);
    }

    private sealed class LedgerFile
    {
        public List<AccountSeed> Accounts { get; set; } = [];

        public List<WithdrawalReceipt> Receipts { get; set; } = [];
    }
}
