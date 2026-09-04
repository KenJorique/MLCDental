using ClinicApp.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace ClinicApp.ViewModels.SupplyVM;

[QueryProperty(nameof(SupplyId), "supplyId")]
[QueryProperty(nameof(HasExpirationParam), "hasExpiration")]
public partial class AddStockViewModel : ObservableObject
{
    private readonly SupabaseDataService _supabase;

    [ObservableProperty] private string supplyId = string.Empty;
    [ObservableProperty] private bool isBusy;

    [ObservableProperty] private bool hasExpirationParam;

    [ObservableProperty] private int addQty;
    [ObservableProperty] private DateTime expirationDate = DateTime.Today.AddYears(1);
    [ObservableProperty] private string qtyError = string.Empty;

    // Injects the shared data service.
    public AddStockViewModel(SupabaseDataService supabase) => _supabase = supabase;

    // Validates quantity, applies the stock change, updates expiration if needed, and logs it.
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
        IsBusy = true;
        try
        {
            var item = await _supabase.GetSupplyByIdAsync(SupplyId);

            await _supabase.ApplyStockChangeAsync(SupplyId, AddQty, "Restocked", string.Empty);

            if (HasExpirationParam && item is not null)
            {
                item.ExpirationDate = ExpirationDate;
                await _supabase.UpdateSupplyAsync(item);
            }

            await _supabase.LogActivityAsync("StockChange",
                $"{item?.Name ?? "Item"} restocked, +{AddQty} pcs");

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

    // Discards and goes back.
    [RelayCommand]
    async Task CancelAsync() => await Shell.Current.GoToAsync("..");
}