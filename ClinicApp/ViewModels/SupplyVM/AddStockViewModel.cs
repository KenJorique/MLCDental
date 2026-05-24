using ClinicApp.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace ClinicApp.ViewModels.SupplyVM;

[QueryProperty(nameof(SupplyId), "supplyId")]
[QueryProperty(nameof(HasExpirationParam), "hasExpiration")]
public partial class AddStockViewModel : ObservableObject
{
    private readonly DatabaseService _db;

    [ObservableProperty] private int supplyId;
    [ObservableProperty] private bool isBusy;

    // Passed from SupplyInfoViewModel so we know whether to show the date picker
    [ObservableProperty] private bool hasExpirationParam;

    [ObservableProperty] private int addQty;
    [ObservableProperty] private DateTime expirationDate = DateTime.Today.AddYears(1);
    [ObservableProperty] private string qtyError = string.Empty;

    public AddStockViewModel(DatabaseService db) => _db = db;

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
            await _db.ApplyStockChange(SupplyId, AddQty, "Restocked", string.Empty);

            // Update expiration on the item if it tracks one
            if (HasExpirationParam)
            {
                var item = await _db.GetSupplyItemById(SupplyId);
                if (item is not null)
                {
                    item.ExpirationDate = ExpirationDate.ToString("yyyy-MM-dd");
                    await _db.UpdateSupplyItem(item);
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
