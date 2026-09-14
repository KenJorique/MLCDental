using ClinicApp.Models.SupabaseModels;
using ClinicApp.Models.TransactionModels;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;

namespace ClinicApp.ViewModels.TransactionVM;

// One card per bill on the Ledger page, pairing a SupabaseBill with its own payment history and expand/collapse state; rebuilt fresh on every load.
public partial class BillCardItem : ObservableObject
{
    // The underlying bill this card represents.
    public SupabaseBill Bill { get; }

    // This bill's payment rows, in display order.
    public ObservableCollection<PaymentRowItem> Payments { get; } = new();

    public bool HasPayments => Payments.Count > 0;
    public bool HasNoPayments => Payments.Count == 0;

    // Expanded by default unless the bill is fully paid, so unpaid bills show their history right away.
    [ObservableProperty]
    bool isExpanded;

    // Formatted display of the bill's remaining balance.
    public string CurrentBalanceDisplay => $"₱{Bill.Balance:N2}";

    // Whether the "Add Payment" button should show for this card.
    public bool ShowAddPayment => Bill.Balance > 0;

    // Chevron glyph, flipped based on expand state.
    public string ToggleIcon => IsExpanded ? "\ue5ce" : "\ue5cf";

    // Human-readable status label for the accent strip / pill.
    public string StatusLabel => Bill.StatusDisplay;

    // Accent strip / pill text color — delegates to Bill so there's one source of truth.
    public Color AccentColor => Bill.StatusColor;

    // Pill background color — delegates to Bill so there's one source of truth.
    public Color StatusBgColor => Bill.StatusBgColor;

    // Wraps the bill and its already-fetched payment rows, defaulting expanded state from the bill's status.
    public BillCardItem(SupabaseBill bill, IEnumerable<PaymentRowItem> payments)
    {
        Bill = bill;
        IsExpanded = !string.Equals(bill.Status, "paid", StringComparison.OrdinalIgnoreCase);

        foreach (var p in payments)
            Payments.Add(p);
    }

    // Flips the card's expand/collapse state.
    [RelayCommand]
    void ToggleExpand() => IsExpanded = !IsExpanded;

    // Keeps the chevron glyph in sync whenever expand state changes.
    partial void OnIsExpandedChanged(bool value) =>
        OnPropertyChanged(nameof(ToggleIcon));
}
