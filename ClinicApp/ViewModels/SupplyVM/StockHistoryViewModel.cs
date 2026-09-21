using ClinicApp.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;

namespace ClinicApp.ViewModels.SupplyVM;

[QueryProperty(nameof(SupplyId), "supplyId")]
public partial class StockHistoryViewModel : ObservableObject
{
    private readonly SupabaseDataService _supabase;

    [ObservableProperty] private string supplyId = string.Empty;
    [ObservableProperty] private bool isBusy;
    [ObservableProperty] private string supplyName = string.Empty;
    [ObservableProperty] private string stockDisplay = string.Empty;
    [ObservableProperty] private string stockStatus = string.Empty;

    // Pale badge background, matching SupplyCardViewModel/BillCardItem's status-pill convention.
    [ObservableProperty] private string stockStatusColor = "#E8F5E9";

    // Saturated badge text color, paired with StockStatusColor above.
    [ObservableProperty] private string stockStatusTextColor = "#2E7D32";

    public ObservableCollection<StockLogRowViewModel> Logs { get; } = new();

    // Shrinks the header name's font as it gets longer, so it stays on one line next to the badge and qty.
    public double NameFontSize => SupplyName.Length switch
    {
        <= 14 => 20,
        <= 20 => 16,
        _ => 15
    };

    // Refreshes NameFontSize whenever the loaded supply's name changes.
    partial void OnSupplyNameChanged(string value) => OnPropertyChanged(nameof(NameFontSize));


    // Injects the shared data service.
    public StockHistoryViewModel(SupabaseDataService supabase) => _supabase = supabase;

    // Loads the supply's history once SupplyId is set via navigation.
    partial void OnSupplyIdChanged(string value)
    {
        if (!string.IsNullOrWhiteSpace(value))
            MainThread.BeginInvokeOnMainThread(async () => await LoadAsync());
    }

    // Loads the supply item's header info and its full stock log history.
    [RelayCommand]
    public async Task LoadAsync()
    {
        if (string.IsNullOrWhiteSpace(SupplyId) || IsBusy) return;
        IsBusy = true;
        try
        {
            var item = await _supabase.GetSupplyByIdAsync(SupplyId);
            if (item is null) return;
            SupplyName = item.Name;
            StockDisplay = item.QuantityDisplay;
            StockStatus = item.IsOutOfStock ? "Out of Stock"
                        : item.IsLowStock ? "Low Stock"
                        : "In Stock";
            StockStatusColor = item.IsOutOfStock ? "#FCEAEA"
                             : item.IsLowStock ? "#FFF3E0"
                             : "#E8F5E9";
            StockStatusTextColor = item.IsOutOfStock ? "#C62828"
                             : item.IsLowStock ? "#E65100"
                             : "#2E7D32";

            var logs = await _supabase.GetLogsForSupplyAsync(SupplyId);
            Logs.Clear();
            foreach (var log in logs)
                Logs.Add(new StockLogRowViewModel(log));
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[StockHistory] Load error: {ex}");
        }
        finally { IsBusy = false; }
    }
}
