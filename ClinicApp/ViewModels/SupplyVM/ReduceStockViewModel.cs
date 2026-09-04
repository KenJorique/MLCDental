using ClinicApp.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;

namespace ClinicApp.ViewModels.SupplyVM;

[QueryProperty(nameof(SupplyId), "supplyId")]
[QueryProperty(nameof(CurrentStock), "currentStock")]
public partial class ReduceStockViewModel : ObservableObject
{
    private readonly SupabaseDataService _supabase;

    [ObservableProperty] private string supplyId = string.Empty;
    [ObservableProperty] private int currentStock;
    [ObservableProperty] private bool isBusy;

    [ObservableProperty] private int reduceQty;
    [ObservableProperty] private string selectedType = "Used";
    [ObservableProperty] private string qtyError = string.Empty;

    public ObservableCollection<string> TypeOptions { get; } = new()
    {
        "Used", "Damaged", "Expired"
    };

    public string MaxAvailableText => $"Maximum available: {CurrentStock} pcs";

    // Injects the shared data service.
    public ReduceStockViewModel(SupabaseDataService supabase) => _supabase = supabase;

    // Refreshes the "Maximum available" text when CurrentStock changes.
    partial void OnCurrentStockChanged(int value) =>
        OnPropertyChanged(nameof(MaxAvailableText));

    // Validates quantity, applies the reduction, and logs it.
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
            var item = await _supabase.GetSupplyByIdAsync(SupplyId);

            await _supabase.ApplyStockChangeAsync(SupplyId, -ReduceQty, SelectedType, string.Empty);

            await _supabase.LogActivityAsync("StockChange",
                $"{item?.Name ?? "Item"} marked {SelectedType.ToLower()}, -{ReduceQty} pcs");

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

    // Discards and goes back.
    [RelayCommand]
    async Task CancelAsync() => await Shell.Current.GoToAsync("..");
}