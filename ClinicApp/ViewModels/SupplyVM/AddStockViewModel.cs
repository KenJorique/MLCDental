using ClinicApp.Services;
using ClinicApp.Views.Shared;
using CommunityToolkit.Maui.Views;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace ClinicApp.ViewModels.SupplyVM;

[QueryProperty(nameof(SupplyId), "supplyId")]
[QueryProperty(nameof(HasExpirationParam), "hasExpiration")]
public partial class AddStockViewModel : ObservableObject
{
    private readonly SupabaseDataService _supabase;
    private bool _isDirty;

    [ObservableProperty] private string supplyId = string.Empty;
    [ObservableProperty] private bool isBusy;

    [ObservableProperty] private bool hasExpirationParam;

    [ObservableProperty] private int addQty;
    [ObservableProperty] private DateTime expirationDate = DateTime.Today.AddYears(1);
    [ObservableProperty] private string qtyError = string.Empty;

    // Injects the shared data service.
    public AddStockViewModel(SupabaseDataService supabase) => _supabase = supabase;

    // Flags the form dirty when the quantity changes.
    partial void OnAddQtyChanged(int value) => _isDirty = true;

    // Flags the form dirty when the expiration date changes.
    partial void OnExpirationDateChanged(DateTime value) => _isDirty = true;

    // Confirms with the popup, then validates, applies the stock change, updates expiration if needed, and logs it.
    [RelayCommand]
    async Task SaveAsync()
    {
        QtyError = string.Empty;
        if (AddQty <= 0)
        {
            QtyError = "Please enter a quantity greater than 0.";
            return;
        }
        if (IsBusy) return;

        var confirmPopup = new ConfirmationPopup(
            "Add Stock?",
            $"Add {AddQty} pcs to stock?",
            confirmText: "Save",
            confirmColor: Colors.Green);

        var confirmResult = await Shell.Current.ShowPopupAsync(confirmPopup);
        if (confirmResult is not bool confirmed || !confirmed) return;

        IsBusy = true;
        try
        {
            var item = await _supabase.GetSupplyByIdAsync(SupplyId);

            // Update expiration first, while the fetched item's quantity still matches
            // the server, so this write can't clobber the stock change applied below.
            if (HasExpirationParam && item is not null)
            {
                item.ExpirationDate = ExpirationDate;
                await _supabase.UpdateSupplyAsync(item);
            }

            await _supabase.ApplyStockChangeAsync(SupplyId, AddQty, "Restocked", string.Empty);

            await _supabase.LogActivityAsync("StockChange",
                $"{item?.Name ?? "Item"} restocked, +{AddQty} pcs");

            _isDirty = false;

            await MainThread.InvokeOnMainThreadAsync(async () =>
                await Shell.Current.GoToAsync(".."));
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[AddStock] Save error: {ex}");
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
