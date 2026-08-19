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

    // "Current Balance" + Add Payment live inside the expanded area for
    // every bill. Paid bills just show ₱0.00 with the button hidden
    // (ShowAddPayment below) — spec section 4.
    public string CurrentBalanceDisplay => $"₱{Bill.Balance:N2}";

    public bool ShowAddPayment => Bill.Balance > 0;

    // MaterialSymbolsRounded: expand_less (\ue5ce) / expand_more (\ue5cf)
    public string ToggleIcon => IsExpanded ? "\ue5ce" : "\ue5cf";

    // Canonical status text — SupabaseBill.StatusDisplay returns "Partial"
    // (used elsewhere, e.g. BillDetailsPage). This page uses "Partial" too
    // (short form, fits the pill better) but "Unpaid" / "Paid" stay spelled
    // out in full, matching what's on the patient summary card above.
    public string StatusLabel => Bill.Status?.ToLowerInvariant() switch
    {
        "paid" => "Paid",
        "partial" => "Partial",
        _ => "Unpaid"
    };

    // Drives the card's left accent strip and the status pill's text
    // color — green/orange/red for Paid/Partial/Unpaid.
    public Color AccentColor => Bill.Status?.ToLowerInvariant() switch
    {
        "paid" => Color.FromArgb("#2E7D32"),
        "partial" => Color.FromArgb("#E65100"),
        _ => Color.FromArgb("#C62828")
    };

    // Pill background — pale tint of the accent color, same pairing
    // used on the patient summary card's status badge.
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
