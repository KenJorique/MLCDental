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

    public AddStockViewModel(SupabaseDataService supabase) => _supabase = supabase;

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
            await _supabase.ApplyStockChangeAsync(SupplyId, AddQty, "Restocked", string.Empty);

            if (HasExpirationParam)
            {
                var item = await _supabase.GetSupplyByIdAsync(SupplyId);
                if (item is not null)
                {
                    item.ExpirationDate = ExpirationDate;
                    await _supabase.UpdateSupplyAsync(item);
                }
            }

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

    [RelayCommand]
    async Task CancelAsync() => await Shell.Current.GoToAsync("..");
}