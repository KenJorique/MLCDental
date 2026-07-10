using ClinicApp.Models;
using CommunityToolkit.Mvvm.ComponentModel;

namespace ClinicApp.ViewModels.SupplyVM;

public partial class SupplyCardViewModel : ObservableObject
{
    [ObservableProperty] private SupplyItem _supply;
    [ObservableProperty] private bool _isExpanded;

    public SupplyCardViewModel(SupplyItem supply) => _supply = supply;

    public string StockDisplay => Supply.QuantityDisplay;
    public bool IsLowStock => Supply.IsLowStock;
    public bool IsOutOfStock => Supply.IsOutOfStock;
    public string UnitDisplay => Supply.Unit ?? "Per Piece";
    public string StockStatusLabel => Supply.IsOutOfStock ? "Out of Stock"
                                    : Supply.IsLowStock ? "Low Stock"
                                    : "In Stock";
    public string StockStatusColor => Supply.IsOutOfStock ? "#D32F2F"
                                    : Supply.IsLowStock ? "#F57C00"
                                    : "#388E3C";
    public string ExpirationDisplay => Supply.HasExpiration && !string.IsNullOrWhiteSpace(Supply.ExpirationDate)
                                        ? Supply.ExpirationDate : "—";

    public void Refresh()
    {
        OnPropertyChanged(nameof(StockDisplay));
        OnPropertyChanged(nameof(IsLowStock));
        OnPropertyChanged(nameof(IsOutOfStock));
        OnPropertyChanged(nameof(UnitDisplay));
        OnPropertyChanged(nameof(StockStatusLabel));
        OnPropertyChanged(nameof(StockStatusColor));
        OnPropertyChanged(nameof(ExpirationDisplay));
    }
}
