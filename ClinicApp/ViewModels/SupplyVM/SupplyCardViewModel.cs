using ClinicApp.Models;
using CommunityToolkit.Mvvm.ComponentModel;

namespace ClinicApp.ViewModels.SupplyVM;

public partial class SupplyCardViewModel : ObservableObject
{
    [ObservableProperty] private SupplyItem _supply;
    [ObservableProperty] private bool _isExpanded;

    public SupplyCardViewModel(SupplyItem supply) => _supply = supply;

    public string StockDisplay => Supply.QuantityDisplay;
    public string UnitDisplay => string.IsNullOrWhiteSpace(Supply.SizeVariant)
                                        ? Supply.Unit : $"{Supply.Unit} · {Supply.SizeVariant}";
    public bool IsLowStock => Supply.IsLowStock;
    public bool IsOutOfStock => Supply.IsOutOfStock;
    public string StockStatusLabel => Supply.IsOutOfStock ? "Out of Stock"
                                    : Supply.IsLowStock ? "Low Stock"
                                    : "In Stock";
    public string ExpirationDisplay => Supply.HasExpiration && !string.IsNullOrWhiteSpace(Supply.ExpirationDate)
                                        ? Supply.ExpirationDate : "—";

    public void Refresh()
    {
        OnPropertyChanged(nameof(StockDisplay));
        OnPropertyChanged(nameof(IsLowStock));
        OnPropertyChanged(nameof(IsOutOfStock));
        OnPropertyChanged(nameof(StockStatusLabel));
        OnPropertyChanged(nameof(UnitDisplay));
        OnPropertyChanged(nameof(ExpirationDisplay));
    }
}
