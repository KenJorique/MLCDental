using ClinicApp.Models;
using CommunityToolkit.Mvvm.ComponentModel;

namespace ClinicApp.ViewModels.SupplyVM;

public partial class SupplyCardViewModel : ObservableObject
{
    [ObservableProperty] private SupabaseSupplyItem _supply;
    [ObservableProperty] private bool _isExpanded;

    public SupplyCardViewModel(SupabaseSupplyItem supply) => _supply = supply;

    public string StockDisplay => Supply.QuantityDisplay;
    public bool IsLowStock => Supply.IsLowStock;
    public bool IsOutOfStock => Supply.IsOutOfStock;
    public string UnitDisplay => Supply.Unit ?? "Per Piece";
    public string StockStatusLabel => Supply.IsOutOfStock ? "Out of Stock"
                                    : Supply.IsLowStock ? "Low Stock"
                                    : "In Stock";

    // Pale background + saturated text, matching BillCardItem's
    // Paid/Partial/Unpaid palette exactly (Ledger page) rather than the
    // old solid-bg/white-text look.
    public string StockStatusColor => Supply.IsOutOfStock ? "#FCEAEA"
                                    : Supply.IsLowStock ? "#FFF3E0"
                                    : "#E8F5E9";

    public string StockStatusTextColor => Supply.IsOutOfStock ? "#C62828"
                                    : Supply.IsLowStock ? "#E65100"
                                    : "#2E7D32";
    public string ExpirationDisplay => Supply.HasExpiration && !string.IsNullOrWhiteSpace(Supply.ExpirationDateDisplay)
                                        ? Supply.ExpirationDateDisplay : "—";

    // Inline expired warning (see SupplyListPage.xaml) — only true for
    // items that actually have an expiration date set AND it's in the past.
    public bool IsExpired => Supply.HasExpiration
                              && Supply.ExpirationDate.HasValue
                              && Supply.ExpirationDate.Value.Date < DateTime.Now.Date;

    public string ExpiredWarningText => $"Expired {Supply.ExpirationDateDisplay}";

    public void Refresh()
    {
        OnPropertyChanged(nameof(StockDisplay));
        OnPropertyChanged(nameof(IsLowStock));
        OnPropertyChanged(nameof(IsOutOfStock));
        OnPropertyChanged(nameof(UnitDisplay));
        OnPropertyChanged(nameof(StockStatusLabel));
        OnPropertyChanged(nameof(StockStatusColor));
        OnPropertyChanged(nameof(StockStatusTextColor));
        OnPropertyChanged(nameof(ExpirationDisplay));
        OnPropertyChanged(nameof(IsExpired));
        OnPropertyChanged(nameof(ExpiredWarningText));
    }
}