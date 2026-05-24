using ClinicApp.Models;
using ClinicApp.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace ClinicApp.ViewModels.SupplyVM;

[QueryProperty(nameof(SupplyId), "supplyId")]
public partial class AddSupplyViewModel : ObservableObject
{
    private readonly DatabaseService _db;
    private SupplyItem? _editing;

    [ObservableProperty] private int supplyId;
    [ObservableProperty] private bool isBusy;
    [ObservableProperty] private bool isEditMode;
    [ObservableProperty] private string pageTitle = "Add New Item";

    // Form fields
    [ObservableProperty] private string itemName = string.Empty;
    [ObservableProperty] private int quantity;
    [ObservableProperty] private string sizeVariant = string.Empty;
    [ObservableProperty] private bool hasExpiration;
    [ObservableProperty] private DateTime expirationDate = DateTime.Today.AddYears(1);
    [ObservableProperty] private int minimumStock = 10;

    // Validation
    [ObservableProperty] private string nameError = string.Empty;
    [ObservableProperty] private bool canSave;

    public AddSupplyViewModel(DatabaseService db) => _db = db;

    partial void OnSupplyIdChanged(int value)
    {
        if (value > 0)
            MainThread.BeginInvokeOnMainThread(async () => await LoadForEditAsync(value));
    }

    partial void OnItemNameChanged(string value) => ValidateForm();
    partial void OnQuantityChanged(int value) => ValidateForm();
    partial void OnMinimumStockChanged(int value) => ValidateForm();

    private async Task LoadForEditAsync(int id)
    {
        try
        {
            var item = await _db.GetSupplyItemById(id);
            if (item is null) return;
            _editing = item;
            IsEditMode = true;
            PageTitle = "Edit Item";

            ItemName = item.Name;
            Quantity = item.QuantityInPieces;
            SizeVariant = item.SizeVariant;
            HasExpiration = item.HasExpiration;
            MinimumStock = item.MinimumStockPieces;

            if (item.HasExpiration && DateTime.TryParse(item.ExpirationDate, out var exp))
                ExpirationDate = exp;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[AddSupply] LoadForEdit error: {ex}");
        }
    }

    private void ValidateForm()
    {
        NameError = string.IsNullOrWhiteSpace(ItemName) ? "Item name is required." : string.Empty;
        CanSave = string.IsNullOrWhiteSpace(NameError) && MinimumStock >= 0;
    }

    [RelayCommand]
    async Task SaveAsync()
    {
        ValidateForm();
        if (!CanSave || IsBusy) return;
        IsBusy = true;
        try
        {
            if (IsEditMode && _editing is not null)
            {
                _editing.Name = ItemName.Trim();
                _editing.QuantityInPieces = Quantity;
                _editing.SizeVariant = SizeVariant.Trim();
                _editing.HasExpiration = HasExpiration;
                _editing.ExpirationDate = HasExpiration
                    ? ExpirationDate.ToString("yyyy-MM-dd") : string.Empty;
                _editing.MinimumStockPieces = MinimumStock;

                await _db.UpdateSupplyItem(_editing);
            }
            else
            {
                // FIX: Save item with QuantityInPieces = 0 first, then apply the
                // stock change separately. This prevents the quantity being doubled
                // (once from the item row, once from ApplyStockChange).
                var item = new SupplyItem
                {
                    Name = ItemName.Trim(),
                    QuantityInPieces = 0,   // always start at 0; stock log sets the real value
                    SizeVariant = SizeVariant.Trim(),
                    HasExpiration = HasExpiration,
                    ExpirationDate = HasExpiration
                        ? ExpirationDate.ToString("yyyy-MM-dd") : string.Empty,
                    MinimumStockPieces = MinimumStock,
                };

                int newId = await _db.AddSupplyItem(item);

                if (newId <= 0)
                {
                    await Shell.Current.DisplayAlert("Error",
                        "Could not save the item. Please try again.", "OK");
                    return;
                }

                // ApplyStockChange both logs the transaction AND updates
                // QuantityInPieces on the item row — so only call it once.
                if (Quantity > 0)
                    await _db.ApplyStockChange(newId, Quantity, "Restocked",
                        "Initial stock on creation");
            }

            await MainThread.InvokeOnMainThreadAsync(async () =>
                await Shell.Current.GoToAsync(".."));
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[AddSupply] Save error: {ex}");
            await Shell.Current.DisplayAlert("Save Failed",
                $"An error occurred: {ex.Message}", "OK");
        }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    async Task CancelAsync() => await Shell.Current.GoToAsync("..");
}
