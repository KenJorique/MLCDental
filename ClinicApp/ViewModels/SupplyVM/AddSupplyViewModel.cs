using ClinicApp.Models;
using ClinicApp.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace ClinicApp.ViewModels.SupplyVM;

[QueryProperty(nameof(SupplyId), "supplyId")]
public partial class AddSupplyViewModel : ObservableObject
{
    private readonly SupabaseDataService _supabase;
    private SupabaseSupplyItem? _editing;

    public List<string> UnitOptions { get; } = new()
    {
        "Per Piece", "Per Pack", "Per Box", "Per Kit"
    };

    [ObservableProperty] private string? supplyId;
    [ObservableProperty] private bool isBusy;
    [ObservableProperty] private bool isEditMode;
    [ObservableProperty] private string pageTitle = "Add New Item";

    [ObservableProperty] private string itemName = string.Empty;
    [ObservableProperty] private string selectedUnit = "Per Piece";
    [ObservableProperty] private int piecesPerUnit = 1;
    [ObservableProperty] private int unitQuantity = 0;
    [ObservableProperty] private bool hasExpiration;
    [ObservableProperty] private DateTime expirationDate = DateTime.Today.AddYears(1);
    [ObservableProperty] private int minimumStock = 10;

    [ObservableProperty] private bool showPiecesPerUnit;
    [ObservableProperty] private int totalPieces;

    [ObservableProperty] private string nameError = string.Empty;
    [ObservableProperty] private bool canSave;

    public AddSupplyViewModel(SupabaseDataService supabase)
    {
        _supabase = supabase;
        selectedUnit = "Per Piece";
        showPiecesPerUnit = false;
    }

    partial void OnSupplyIdChanged(string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
            MainThread.BeginInvokeOnMainThread(async () => await LoadForEditAsync(value));
    }

    partial void OnItemNameChanged(string value) => ValidateForm();
    partial void OnMinimumStockChanged(int value) => ValidateForm();

    partial void OnSelectedUnitChanged(string value)
    {
        ShowPiecesPerUnit = value != "Per Piece";
        if (!ShowPiecesPerUnit)
            PiecesPerUnit = 1;
        RecalculateTotal();
        ValidateForm();
    }

    partial void OnPiecesPerUnitChanged(int value)
    {
        RecalculateTotal();
        ValidateForm();
    }

    partial void OnUnitQuantityChanged(int value)
    {
        RecalculateTotal();
        ValidateForm();
    }

    private void RecalculateTotal()
    {
        TotalPieces = ShowPiecesPerUnit
            ? PiecesPerUnit * UnitQuantity
            : UnitQuantity;
    }

    private async Task LoadForEditAsync(string id)
    {
        try
        {
            var item = await _supabase.GetSupplyByIdAsync(id);
            if (item is null) return;
            _editing = item;
            IsEditMode = true;
            PageTitle = "Edit Item";

            ItemName = item.Name;
            SelectedUnit = item.Unit;
            PiecesPerUnit = item.PiecesPerUnit;
            HasExpiration = item.HasExpiration;
            MinimumStock = item.MinimumStockPieces;

            if (item.HasExpiration && item.ExpirationDate.HasValue)
                ExpirationDate = item.ExpirationDate.Value;
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
                _editing.Unit = SelectedUnit;
                _editing.PiecesPerUnit = SelectedUnit == "Per Piece" ? 1 : PiecesPerUnit;
                _editing.HasExpiration = HasExpiration;
                _editing.ExpirationDate = HasExpiration ? ExpirationDate : null;
                _editing.MinimumStockPieces = MinimumStock;

                var success = await _supabase.UpdateSupplyAsync(_editing);
                if (!success)
                {
                    await Shell.Current.DisplayAlert("Error", "Could not update the item.", "OK");
                    return;
                }
            }
            else
            {
                int pieces = SelectedUnit == "Per Piece" ? 1 : PiecesPerUnit;

                var newItem = await _supabase.AddSupplyAsync(new SupabaseSupplyItem
                {
                    Name = ItemName.Trim(),
                    Unit = SelectedUnit,
                    PiecesPerUnit = pieces,
                    QuantityInPieces = 0,
                    HasExpiration = HasExpiration,
                    ExpirationDate = HasExpiration ? ExpirationDate : null,
                    MinimumStockPieces = MinimumStock,
                    AddedDate = DateTime.Today,
                    CreatedAt = DateTime.UtcNow
                });

                if (newItem is null)
                {
                    await Shell.Current.DisplayAlert("Error",
                        "Could not save the item. Please try again.", "OK");
                    return;
                }

                if (TotalPieces > 0)
                    await _supabase.ApplyStockChangeAsync(newItem.Id, TotalPieces, "Restocked",
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