using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using Salonera.Application.Features.Payments;
using Salonera.Application.Features.Finance;
using Salonera.Application.Common.Security;
using Salonera.Domain.Enums;

namespace Salonera.Desktop.ViewModels;

public sealed class PaymentViewModel(IPaymentService payments, IReceiptPdfService receiptPdf,
    IFinanceService finance, ICurrentUser currentUser) : ObservableObject
{
    private static readonly CultureInfo Turkish = CultureInfo.GetCultureInfo("tr-TR");
    private ContractPaymentListItem? _selectedContract;
    private InstallmentItem? _selectedInstallment;
    private ReceiptItem? _selectedReceipt;
    private string _total = "—";
    private string _collected = "—";
    private string _refunded = "—";
    private string _remaining = "—";
    private string _amount = string.Empty;
    private string _exchangeRate = "1";
    private string _planAmount = string.Empty;
    private DateTime? _planDate = DateTime.Today;
    private string _refundAmount = string.Empty;
    private string _reason = string.Empty;
    private string _message = string.Empty;
    private PaymentMethodOption? _selectedMethod;
    private FinancialAccountItem? _selectedAccount;
    private string? _lastPdfPath;

    public ObservableCollection<ContractPaymentListItem> Contracts { get; } = [];
    public ObservableCollection<InstallmentItem> Installments { get; } = [];
    public ObservableCollection<ReceiptItem> Receipts { get; } = [];
    public ObservableCollection<FinancialAccountItem> Accounts { get; } = [];
    public ObservableCollection<NewInstallment> PlanDraft { get; } = [];
    public IReadOnlyList<PaymentMethodOption> Methods { get; } =
    [
        new(PaymentMethod.Cash, "Nakit"), new(PaymentMethod.Bank, "Banka"),
        new(PaymentMethod.CreditCard, "Kredi kartı"), new(PaymentMethod.Other, "Diğer"),
    ];
    public bool CanManageFinance { get; } =
        currentUser.User?.Permissions.Contains(Permissions.FinanceManage) == true;
    public ContractPaymentListItem? SelectedContract { get => _selectedContract; set => SetProperty(ref _selectedContract, value); }
    public InstallmentItem? SelectedInstallment { get => _selectedInstallment; set => SetProperty(ref _selectedInstallment, value); }
    public ReceiptItem? SelectedReceipt { get => _selectedReceipt; set => SetProperty(ref _selectedReceipt, value); }
    public string Total { get => _total; private set => SetProperty(ref _total, value); }
    public string Collected { get => _collected; private set => SetProperty(ref _collected, value); }
    public string Refunded { get => _refunded; private set => SetProperty(ref _refunded, value); }
    public string Remaining { get => _remaining; private set => SetProperty(ref _remaining, value); }
    public string Amount { get => _amount; set => SetProperty(ref _amount, value); }
    public string ExchangeRate { get => _exchangeRate; set => SetProperty(ref _exchangeRate, value); }
    public string PlanAmount { get => _planAmount; set => SetProperty(ref _planAmount, value); }
    public DateTime? PlanDate { get => _planDate; set => SetProperty(ref _planDate, value); }
    public string RefundAmount { get => _refundAmount; set => SetProperty(ref _refundAmount, value); }
    public string Reason { get => _reason; set => SetProperty(ref _reason, value); }
    public string Message { get => _message; private set => SetProperty(ref _message, value); }
    public PaymentMethodOption? SelectedMethod
    {
        get => _selectedMethod ?? Methods[0];
        set => SetProperty(ref _selectedMethod, value);
    }
    public FinancialAccountItem? SelectedAccount
    {
        get => _selectedAccount;
        set => SetProperty(ref _selectedAccount, value);
    }
    public string? LastPdfPath { get => _lastPdfPath; private set => SetProperty(ref _lastPdfPath, value); }

    public async Task LoadAsync()
    {
        try
        {
            var previousId = SelectedContract?.Id;
            Replace(Contracts, await payments.ListContractsAsync());
            Replace(Accounts, (await finance.ListAccountsAsync()).Where(x => x.IsActive));
            SelectedContract = Contracts.FirstOrDefault(x => x.Id == previousId) ?? Contracts.FirstOrDefault();
            await RefreshAsync();
        }
        catch (Exception exception) { Message = exception.Message; }
    }

    public async Task RefreshAsync()
    {
        if (SelectedContract is null)
        {
            Replace(Installments, []);
            Replace(Receipts, []);
            Total = Collected = Refunded = Remaining = "—";
            Message = "Önce bir sözleşme oluşturun.";
            return;
        }
        try
        {
            var balance = await payments.GetBalanceAsync(SelectedContract.Id);
            var currency = balance.Currency.ToString().ToUpperInvariant();
            Total = Money(balance.Total, currency);
            Collected = Money(balance.Collected, currency);
            Refunded = Money(balance.Refunded, currency);
            Remaining = Money(balance.Remaining, currency);
            Replace(Installments, balance.Installments);
            Replace(Receipts, balance.Receipts);
            SelectedInstallment = null;
            SelectedReceipt = null;
            ExchangeRate = balance.Currency == CurrencyCode.Try ? "1" : ExchangeRate;
            Message = balance.Installments.Any(x => x.Status == PaymentStatus.Overdue)
                ? "Gecikmiş taksit bulunuyor." : string.Empty;
        }
        catch (Exception exception) { Message = exception.Message; }
    }

    public void AddPlanItem()
    {
        if (!PlanDate.HasValue || !TryAmount(PlanAmount, out var amount) || amount <= 0)
        {
            Message = "Vade tarihi ve geçerli bir taksit tutarı girin.";
            return;
        }
        PlanDraft.Add(new NewInstallment(DateOnly.FromDateTime(PlanDate.Value), amount));
        PlanAmount = string.Empty;
        Message = "Taksit taslağa eklendi. Toplam sözleşme tutarına eşit olunca planı kaydedin.";
    }

    public void ClearPlan() => PlanDraft.Clear();

    public async Task SavePlanAsync()
    {
        if (SelectedContract is null) return;
        try
        {
            await payments.SetPlanAsync(SelectedContract.Id, PlanDraft.ToArray());
            PlanDraft.Clear();
            await RefreshAsync();
            Message = "Taksit planı kaydedildi.";
        }
        catch (Exception exception) { Message = exception.Message; }
    }

    public async Task CollectAsync()
    {
        if (SelectedContract is null || !TryAmount(Amount, out var amount) || amount <= 0 ||
            !TryAmount(ExchangeRate, out var rate) || rate <= 0)
        {
            Message = "Tahsilat tutarını ve kuru kontrol edin.";
            return;
        }
        try
        {
            var receiptId = await payments.CollectAsync(new CollectPaymentRequest(
                SelectedContract.Id, SelectedInstallment?.Id, amount, rate,
                SelectedMethod?.Value ?? PaymentMethod.Cash, Guid.NewGuid(),
                SelectedAccount?.Id));
            await RefreshAsync();
            Amount = string.Empty;
            SelectedReceipt = Receipts.FirstOrDefault(x => x.Id == receiptId);
            var pdfPath = await CreatePdfAsync();
            Message = pdfPath is null
                ? "Tahsilat kaydedildi, ancak makbuz PDF oluşturulamadı. Makbuzu seçip yeniden deneyin."
                : "Tahsilat ve makbuz PDF oluşturuldu. Makbuzu listeden açabilirsiniz.";
        }
        catch (Exception exception) { Message = exception.Message; }
    }

    public async Task<string?> CreatePdfAsync()
    {
        if (SelectedReceipt is null) { Message = "Önce bir makbuz seçin."; return null; }
        try
        {
            var path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Salonera", "Makbuzlar", $"makbuz-{SelectedReceipt.Id:N}.pdf");
            LastPdfPath = await receiptPdf.GenerateAsync(SelectedReceipt.Id, path);
            Message = "Makbuz PDF oluşturuldu.";
            return LastPdfPath;
        }
        catch (Exception exception) { Message = exception.Message; return null; }
    }

    public async Task CancelAsync()
    {
        if (SelectedReceipt is null || string.IsNullOrWhiteSpace(Reason))
        { Message = "Makbuz ve iptal nedeni seçin."; return; }
        if (SelectedReceipt.IsCancelled)
        { Message = "Bu tahsilat zaten iptal edilmiş."; return; }
        try
        {
            await payments.CancelReceiptAsync(SelectedReceipt.Id, Reason);
            await RefreshAsync();
            Reason = string.Empty;
            Message = "Tahsilat iptal edildi; finansal ters kayıt oluşturuldu.";
        }
        catch (Exception exception) { Message = exception.Message; }
    }

    public async Task RefundAsync()
    {
        if (SelectedReceipt is null || !TryAmount(RefundAmount, out var amount) || amount <= 0 ||
            string.IsNullOrWhiteSpace(Reason))
        { Message = "Makbuz, iade tutarı ve neden girin."; return; }
        try
        {
            await payments.RefundAsync(new RefundPaymentRequest(SelectedReceipt.Id, amount, Reason));
            await RefreshAsync();
            RefundAmount = Reason = string.Empty;
            Message = "İade kaydedildi; kasa/banka çıkışı oluşturuldu.";
        }
        catch (Exception exception) { Message = exception.Message; }
    }

    private static bool TryAmount(string value, out decimal amount) =>
        decimal.TryParse(value, NumberStyles.Number, Turkish, out amount) ||
        decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out amount);
    private static string Money(decimal amount, string currency) => $"{amount.ToString("N2", Turkish)} {currency}";
    private static void Replace<T>(ObservableCollection<T> target, IEnumerable<T> source)
    {
        target.Clear();
        foreach (var item in source) target.Add(item);
    }
}
