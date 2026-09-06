using ClinicApp.Models.SupabaseModels;
using ClinicApp.Services;
using ClinicApp.Views.Shared;
using CommunityToolkit.Maui.Views;
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

    // Tracks whether the user has made any unsaved edits, so Cancel / the
    // back arrow know whether a "discard changes?" prompt is actually needed.
    private bool _isLoading;
    private bool _isDirty;

    // Injects the shared data service.
    public AddSupplyViewModel(SupabaseDataService supabase)
    {
        _supabase = supabase;
        selectedUnit = "Per Piece";
        showPiecesPerUnit = false;
    }

    // Loads the item for editing once SupplyId is set via navigation.
    partial void OnSupplyIdChanged(string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
            MainThread.BeginInvokeOnMainThread(async () => await LoadForEditAsync(value));
    }

    // Re-validates and flags the form dirty on name changes.
    partial void OnItemNameChanged(string value)
    {
        MarkDirty();
        ValidateForm();
    }

    // Re-validates and flags the form dirty on minimum-stock changes.
    partial void OnMinimumStockChanged(int value)
    {
        MarkDirty();
        ValidateForm();
    }

    // Toggles the pieces-per-unit field, recalculates total pieces, and flags the form dirty.
    partial void OnSelectedUnitChanged(string value)
    {
        MarkDirty();
        ShowPiecesPerUnit = value != "Per Piece";
        if (!ShowPiecesPerUnit)
            PiecesPerUnit = 1;
        RecalculateTotal();
        ValidateForm();
    }

    // Recalculates total pieces and flags the form dirty on pieces-per-unit changes.
    partial void OnPiecesPerUnitChanged(int value)
    {
        MarkDirty();
        RecalculateTotal();
        ValidateForm();
    }

    // Recalculates total pieces and flags the form dirty on unit-quantity changes.
    partial void OnUnitQuantityChanged(int value)
    {
        MarkDirty();
        RecalculateTotal();
        ValidateForm();
    }

    // Flags the form dirty when the expiration toggle changes.
    partial void OnHasExpirationChanged(bool value) => MarkDirty();

    // Flags the form dirty when the expiration date changes.
    partial void OnExpirationDateChanged(DateTime value) => MarkDirty();

    // Marks the form dirty, unless we're still loading initial data.
    private void MarkDirty()
    {
        if (!_isLoading)
            _isDirty = true;
    }

    // Computes TotalPieces from unit quantity and pieces-per-unit.
    private void RecalculateTotal()
    {
        TotalPieces = ShowPiecesPerUnit
            ? PiecesPerUnit * UnitQuantity
            : UnitQuantity;
    }

    // Loads an existing item's fields into the form for editing.
    private async Task LoadForEditAsync(string id)
    {
        try
        {
            var item = await _supabase.GetSupplyByIdAsync(id);
            if (item is null) return;
            _editing = item;
            IsEditMode = true;
            PageTitle = "Edit Item";

            _isLoading = true;
            ItemName = item.Name;
            SelectedUnit = item.Unit;
            PiecesPerUnit = item.PiecesPerUnit;
            HasExpiration = item.HasExpiration;
            MinimumStock = item.MinimumStockPieces;

            if (item.HasExpiration && item.ExpirationDate.HasValue)
                ExpirationDate = item.ExpirationDate.Value;
            _isLoading = false;
            _isDirty = false; // freshly loaded data isn't a user edit
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[AddSupply] LoadForEdit error: {ex}");
            _isLoading = false;
        }
    }

    // Requires a name and a non-negative minimum stock.
    private void ValidateForm()
    {
        NameError = string.IsNullOrWhiteSpace(ItemName) ? "Item name is required." : string.Empty;
        CanSave = string.IsNullOrWhiteSpace(NameError) && MinimumStock >= 0;
    }

    // Confirms with the popup, then saves the item (update or create), applies initial stock, and logs new items.
    [RelayCommand]
    async Task SaveAsync()
    {
        ValidateForm();
        if (!CanSave || IsBusy) return;

        var confirmPopup = new ConfirmationPopup(
            "Save Item?",
            IsEditMode
                ? $"Save changes to \"{ItemName.Trim()}\"?"
                : $"Add \"{ItemName.Trim()}\" as a new supply item?",
            confirmText: "Save",
            confirmColor: Colors.Green);

        var confirmResult = await Shell.Current.ShowPopupAsync(confirmPopup);
        if (confirmResult is not bool confirmed || !confirmed) return;

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

                await _supabase.LogActivityAsync("NewSupplyItem", $"New item {newItem.Name} added");
            }

            _isDirty = false;

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
