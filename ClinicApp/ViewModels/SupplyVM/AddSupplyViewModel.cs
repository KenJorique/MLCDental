using ClinicApp.Models;
using ClinicApp.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;

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
    [ObservableProperty] private string selectedUnit = "Piece";
    [ObservableProperty] private int piecesPerUnit = 1;
    [ObservableProperty] private int quantity;
    [ObservableProperty] private string sizeVariant = string.Empty;
    [ObservableProperty] private bool hasExpiration;
    [ObservableProperty] private DateTime expirationDate = DateTime.Today.AddYears(1);
    [ObservableProperty] private int minimumStock = 10;

    // Validation
    [ObservableProperty] private string nameError = string.Empty;
    [ObservableProperty] private bool canSave;

    public ObservableCollection<string> UnitOptions { get; } = new()
    {
        "Piece", "Box", "Pack", "Bottle", "Roll", "Pair", "Set", "Vial", "Tube", "Bag"
    };

    public ObservableCollection<string> NameSuggestions { get; } = new();

    private static readonly string[] _dentalSupplies =
    {
        "Disposable Gloves", "Face Mask", "Dental Bib", "Cotton Rolls", "Gauze Pads",
        "Saliva Ejector", "Dental Mirror", "Dental Explorer", "Dental Forceps",
        "Composite Resin", "Dental Cement", "Dental Amalgam", "Temporary Filling",
        "Articulating Paper", "Impression Material", "Dental Floss", "Tongue Depressor",
        "Hydrogen Peroxide", "Chlorhexidine Mouthwash", "Fluoride Gel", "Topical Anesthetic",
        "Local Anesthetic Cartridges", "Dental Needles", "Suture Material",
        "Rubber Dam", "Dental Wax", "Orthodontic Brackets", "Orthodontic Wire",
        "Bleaching Gel", "Dental X-Ray Film", "Sterilization Pouches",
        "Alcohol Swabs", "Disinfectant Spray", "Handpiece Lubricant"
    };

    public AddSupplyViewModel(DatabaseService db) => _db = db;

    // FIX 1: Use MainThread.BeginInvokeOnMainThread to avoid deadlock crash
    // when QueryProperty fires OnSupplyIdChanged from the navigation thread.
    partial void OnSupplyIdChanged(int value)
    {
        if (value > 0)
            MainThread.BeginInvokeOnMainThread(async () => await LoadForEditAsync(value));
    }

    partial void OnItemNameChanged(string value) { ValidateForm(); UpdateSuggestions(value); }
    partial void OnQuantityChanged(int value) => ValidateForm();
    partial void OnPiecesPerUnitChanged(int value) => ValidateForm();
    partial void OnMinimumStockChanged(int value) => ValidateForm();

    private async Task LoadForEditAsync(int id)
    {
        // FIX 2: Wrap DB load in try/catch — unhandled exception here crashes the app silently
        try
        {
            var item = await _db.GetSupplyItemById(id);
            if (item is null) return;
            _editing = item;
            IsEditMode = true;
            PageTitle = "Edit Item";

            ItemName = item.Name;
            SelectedUnit = item.Unit;
            PiecesPerUnit = item.PiecesPerUnit;
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

    private void UpdateSuggestions(string text)
    {
        NameSuggestions.Clear();
        if (string.IsNullOrWhiteSpace(text) || text.Length < 2) return;
        var q = text.ToLowerInvariant();
        foreach (var s in _dentalSupplies)
            if (s.ToLowerInvariant().Contains(q))
                NameSuggestions.Add(s);
    }

    [RelayCommand]
    void SelectSuggestion(string name) { ItemName = name; NameSuggestions.Clear(); }

    private void ValidateForm()
    {
        NameError = string.IsNullOrWhiteSpace(ItemName) ? "Item name is required." : string.Empty;
        CanSave = string.IsNullOrWhiteSpace(NameError) && PiecesPerUnit >= 1 && MinimumStock >= 0;
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
                _editing.Unit = SelectedUnit;
                _editing.PiecesPerUnit = PiecesPerUnit;
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
                var item = new SupplyItem
                {
                    Name = ItemName.Trim(),
                    Unit = SelectedUnit,
                    PiecesPerUnit = PiecesPerUnit,
                    QuantityInPieces = Quantity,
                    SizeVariant = SizeVariant.Trim(),
                    HasExpiration = HasExpiration,
                    ExpirationDate = HasExpiration
                        ? ExpirationDate.ToString("yyyy-MM-dd") : string.Empty,
                    MinimumStockPieces = MinimumStock,
                };

                // FIX 3: Await AddSupplyItem fully before calling ApplyStockChange.
                // The original code used the return value directly but if AddSupplyItem
                // threw (e.g. duplicate name constraint), ApplyStockChange was called
                // with id=0 which crashed the app.
                int newId = await _db.AddSupplyItem(item);

                if (newId <= 0)
                {
                    await Shell.Current.DisplayAlert("Error",
                        "Could not save the item. Please try again.", "OK");
                    return;
                }

                if (Quantity > 0)
                    await _db.ApplyStockChange(newId, Quantity, "Restocked",
                        "Initial stock on creation");
            }

            // FIX 4: GoToAsync("..") must run on the main thread.
            // Calling it directly from an async Task that was started off-thread crashes Android.
            await MainThread.InvokeOnMainThreadAsync(async () =>
                await Shell.Current.GoToAsync(".."));
        }
        catch (Exception ex)
        {
            // Show the error instead of crashing silently
            System.Diagnostics.Debug.WriteLine($"[AddSupply] Save error: {ex}");
            await Shell.Current.DisplayAlert("Save Failed",
                $"An error occurred: {ex.Message}", "OK");
        }
        finally { IsBusy = false; }
    }

    // Automatically resets PiecesPerUnit to 1 if the user selects "Piece" or units that don't use packaging calculations
    partial void OnSelectedUnitChanged(string value)
    {
        if (value != "Box" && value != "Pack")
        {
            PiecesPerUnit = 1;
        }
        ValidateForm();
    }

    [RelayCommand]
    async Task CancelAsync() => await Shell.Current.GoToAsync("..");
}
