using ClinicApp.Models;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;

namespace ClinicApp.ViewModels.TransactionVM;

// One card per bill on the redesigned Ledger page. Wraps the raw
// SupabaseBill together with its own payment history (fetched
// separately, per bill) and local UI state (expand/collapse). Nothing
// here is persisted — it's rebuilt fresh every time LoadBillsAsync runs.
public partial class BillCardItem : ObservableObject
{
    public SupabaseBill Bill { get; }

    public ObservableCollection<PaymentRowItem> Payments { get; } = new();

    public bool HasPayments => Payments.Count > 0;
    public bool HasNoPayments => Payments.Count == 0;

    // Expanded by default unless the bill is fully paid — so staff see
    // payment history immediately on anything still owing, while paid
    // bills collapse to keep the page short (spec section 3).
    [ObservableProperty]
    bool isExpanded;

    public string CurrentBalanceDisplay => $"₱{Bill.Balance:N2}";

    public bool ShowAddPayment => Bill.Balance > 0;

    public string ToggleIcon => IsExpanded ? "\ue5ce" : "\ue5cf";

    public string StatusLabel => Bill.Status?.ToLowerInvariant() switch
    {
        "paid" => "Paid",
        "partial" => "Partial",
        _ => "Unpaid"
    };

    public Color AccentColor => Bill.Status?.ToLowerInvariant() switch
    {
        "paid" => Color.FromArgb("#2E7D32"),
        "partial" => Color.FromArgb("#E65100"),
        _ => Color.FromArgb("#C62828")
    };

    public Color StatusBgColor => Bill.Status?.ToLowerInvariant() switch
    {
        "paid" => Color.FromArgb("#E8F5E9"),
        "partial" => Color.FromArgb("#FFF3E0"),
        _ => Color.FromArgb("#FCEAEA")
    };

    public BillCardItem(SupabaseBill bill, IEnumerable<PaymentRowItem> payments)
    {
        Bill = bill;
        IsExpanded = !string.Equals(bill.Status, "paid", StringComparison.OrdinalIgnoreCase);

        foreach (var p in payments)
            Payments.Add(p);
    }

    [RelayCommand]
    void ToggleExpand() => IsExpanded = !IsExpanded;

    partial void OnIsExpandedChanged(bool value) =>
        OnPropertyChanged(nameof(ToggleIcon));
}
