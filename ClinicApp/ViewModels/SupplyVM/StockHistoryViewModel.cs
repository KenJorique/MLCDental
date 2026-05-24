using ClinicApp.Models;
using ClinicApp.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;

namespace ClinicApp.ViewModels.SupplyVM;

[QueryProperty(nameof(SupplyId), "supplyId")]
public partial class StockHistoryViewModel : ObservableObject
{
    private readonly DatabaseService _db;

    [ObservableProperty] private int supplyId;
    [ObservableProperty] private bool isBusy;
    [ObservableProperty] private string supplyName = string.Empty;
    [ObservableProperty] private string stockDisplay = string.Empty;
    [ObservableProperty] private string stockStatus = string.Empty;
    [ObservableProperty] private string stockStatusColor = "#388E3C";

    public ObservableCollection<StockLogRowViewModel> Logs { get; } = new();

    public StockHistoryViewModel(DatabaseService db) => _db = db;

    partial void OnSupplyIdChanged(int value)
    {
        if (value > 0)
            MainThread.BeginInvokeOnMainThread(async () => await LoadAsync());
    }

    [RelayCommand]
    public async Task LoadAsync()
    {
        if (SupplyId <= 0 || IsBusy) return;
        IsBusy = true;
        try
        {
            var item = await _db.GetSupplyItemById(SupplyId);
            if (item is null) return;

            SupplyName = item.Name;
            StockDisplay = item.QuantityDisplay;
            StockStatus = item.IsOutOfStock ? "Out of Stock"
                        : item.IsLowStock ? "Low Stock"
                        : "In Stock";
            StockStatusColor = item.IsOutOfStock ? "#D32F2F"
                             : item.IsLowStock ? "#F57C00"
                             : "#388E3C";

            var logs = await _db.GetLogsForSupplyItem(SupplyId);
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
