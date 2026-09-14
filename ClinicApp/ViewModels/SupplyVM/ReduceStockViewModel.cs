using ClinicApp.Services;
using ClinicApp.Views.Shared;
using CommunityToolkit.Maui.Views;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;

namespace ClinicApp.ViewModels.SupplyVM;

[QueryProperty(nameof(SupplyId), "supplyId")]
[QueryProperty(nameof(CurrentStock), "currentStock")]
public partial class ReduceStockViewModel : ObservableObject
{
    private readonly SupabaseDataService _supabase;
    private bool _isDirty;

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

    // Flags the form dirty when the quantity changes.
    partial void OnReduceQtyChanged(int value) => _isDirty = true;

    // Flags the form dirty when the reduction type changes.
    partial void OnSelectedTypeChanged(string value) => _isDirty = true;

    // Confirms with the popup, then validates, applies the reduction, and logs it.
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

        var confirmPopup = new ConfirmationPopup(
            "Reduce Stock?",
            $"Reduce stock by {ReduceQty} pcs ({SelectedType})?",
            confirmText: "Save",
            confirmColor: Colors.Green);

        var confirmResult = await Shell.Current.ShowPopupAsync(confirmPopup);
        if (confirmResult is not bool confirmed || !confirmed) return;

        IsBusy = true;
        try
        {
            var item = await _supabase.GetSupplyByIdAsync(SupplyId);

            await _supabase.ApplyStockChangeAsync(SupplyId, -ReduceQty, SelectedType, string.Empty);

            await _supabase.LogActivityAsync("StockChange",
                $"{item?.Name ?? "Item"} marked {SelectedType.ToLower()}, -{ReduceQty} pcs");

            _isDirty = false;

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

    // Confirms discard with the popup if there are unsaved edits, then goes back.
    [RelayCommand]
    async Task CancelAsync()
    {
        if (_isDirty)
        {
            var popup = new ConfirmationPopup(
                "Discard Changes?",
                "Are you sure you want to discard the changes you made?",
                confirmText: "Discard",
                confirmColor: Colors.Crimson);

            var result = await Shell.Current.ShowPopupAsync(popup);
            if (result is not bool discard || !discard)
                return;
        }

        await Shell.Current.GoToAsync("..");
    }
}
