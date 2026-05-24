using ClinicApp.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;

namespace ClinicApp.ViewModels.SupplyVM;

[QueryProperty(nameof(SupplyId), "supplyId")]
[QueryProperty(nameof(CurrentStock), "currentStock")]
public partial class ReduceStockViewModel : ObservableObject
{
    private readonly DatabaseService _db;

    [ObservableProperty] private int supplyId;
    [ObservableProperty] private int currentStock;
    [ObservableProperty] private bool isBusy;

    [ObservableProperty] private int reduceQty;
    [ObservableProperty] private string selectedType = "Used";
    [ObservableProperty] private string qtyError = string.Empty;

    public ObservableCollection<string> TypeOptions { get; } = new()
    {
        "Used", "Damaged", "Expired"
    };

    // Shows "Maximum available: X pcs" below the entry
    public string MaxAvailableText => $"Maximum available: {CurrentStock} pcs";

    public ReduceStockViewModel(DatabaseService db) => _db = db;

    partial void OnCurrentStockChanged(int value) =>
        OnPropertyChanged(nameof(MaxAvailableText));

    [RelayCommand]
    async Task SaveAsync()
    {
        QtyError = string.Empty;
        if (ReduceQty <= 0)
        {
            QtyError = "Please enter a quantity greater than 0.";
            return;
        }
        if (ReduceQty > CurrentStock)
        {
            QtyError = $"Cannot reduce by more than current stock ({CurrentStock} pcs).";
            return;
        }
        if (IsBusy) return;
        IsBusy = true;
        try
        {
            await _db.ApplyStockChange(SupplyId, -ReduceQty, SelectedType, string.Empty);

            await MainThread.InvokeOnMainThreadAsync(async () =>
                await Shell.Current.GoToAsync(".."));
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[ReduceStock] Save error: {ex}");
            await Shell.Current.DisplayAlert("Error", ex.Message, "OK");
        }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    async Task CancelAsync() => await Shell.Current.GoToAsync("..");
}
