using ClinicApp.Models;
using ClinicApp.Services;
using ClinicApp.Views.SupplyRelated;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;

namespace ClinicApp.ViewModels.SupplyVM;

[QueryProperty(nameof(SupplyId), "supplyId")]
public partial class SupplyInfoViewModel : ObservableObject
{
    private readonly DatabaseService _db;

    [ObservableProperty] private int supplyId;
    [ObservableProperty] private bool isBusy;
    [ObservableProperty] private SupplyItem? supply;

    public ObservableCollection<StockLogRowViewModel> Logs { get; } = new();

    // Add Stock modal
    [ObservableProperty] private bool isAddStockVisible;
    [ObservableProperty] private int addQty;
    [ObservableProperty] private string addExpiration = string.Empty;
    [ObservableProperty] private string addNote = string.Empty;

    // Reduce Stock modal
    [ObservableProperty] private bool isReduceStockVisible;
    [ObservableProperty] private int reduceQty;
    [ObservableProperty] private string reduceNote = string.Empty;
    [ObservableProperty] private string reduceError = string.Empty;
    [ObservableProperty] private string maxAvailableText = string.Empty;

    // Computed display
    public string StockDisplay => Supply?.QuantityDisplay ?? "—";
    public bool IsLowStock => Supply?.IsLowStock ?? false;
    public bool IsOutOfStock => Supply?.IsOutOfStock ?? false;
    public string StockStatus => IsOutOfStock ? "Out of Stock" : IsLowStock ? "Low Stock" : "In Stock";
    public string ExpirationDisplay => Supply is null ? "—"
        : Supply.HasExpiration && !string.IsNullOrWhiteSpace(Supply.ExpirationDate)
            ? Supply.ExpirationDate : "—";

    public ObservableCollection<string> NoteSuggestions { get; } = new()
    {
        "Prophylaxis", "Extraction", "Restoration / Filling", "Root Canal Treatment",
        "Crown Placement", "Braces Adjustment", "Orthodontic Procedure",
        "Teeth Whitening", "Dental X-Ray", "Emergency Treatment",
        "Implant Procedure", "Bridge Placement", "Scaling / Cleaning",
        "Fluoride Treatment", "Expired / Damaged"
    };

    public SupplyInfoViewModel(DatabaseService db) => _db = db;

    partial void OnSupplyIdChanged(int value)
    {
        if (value > 0)
            MainThread.BeginInvokeOnMainThread(async () => await LoadAsync());
    }

    [RelayCommand]
    public async Task LoadAsync()
    {
        if (SupplyId <= 0 || IsBusy) return;
        IsBusy = true;
        try
        {
            Supply = await _db.GetSupplyItemById(SupplyId);
            if (Supply is null) return;
            NotifyDisplayChanged();

            var logs = await _db.GetLogsForSupplyItem(SupplyId);
            Logs.Clear();
            foreach (var log in logs)
                Logs.Add(new StockLogRowViewModel(log));
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[SupplyInfo] Load error: {ex}");
        }
        finally { IsBusy = false; }
    }

    private void NotifyDisplayChanged()
    {
        OnPropertyChanged(nameof(StockDisplay));
        OnPropertyChanged(nameof(IsLowStock));
        OnPropertyChanged(nameof(IsOutOfStock));
        OnPropertyChanged(nameof(StockStatus));
        OnPropertyChanged(nameof(ExpirationDisplay));
        MaxAvailableText = $"Maximum available: {Supply?.QuantityInPieces} pcs";
    }

    // ── Add Stock ────────────────────────────────────────────────────

    [RelayCommand]
    void ShowAddStock()
    {
        AddQty = 0; AddExpiration = string.Empty; AddNote = string.Empty;
        IsReduceStockVisible = false;
        IsAddStockVisible = true;
    }

    [RelayCommand]
    void HideAddStock() => IsAddStockVisible = false;

    [RelayCommand]
    async Task ConfirmAddStockAsync()
    {
        if (AddQty <= 0 || IsBusy) return;
        IsBusy = true;
        try
        {
            await _db.ApplyStockChange(SupplyId, AddQty, "Restocked", AddNote);

            if (!string.IsNullOrWhiteSpace(AddExpiration) && Supply is not null)
            {
                Supply.HasExpiration = true;
                Supply.ExpirationDate = AddExpiration;
                await _db.UpdateSupplyItem(Supply);
            }
            IsAddStockVisible = false;
            await LoadAsync();
        }
        finally { IsBusy = false; }
    }

    // ── Reduce Stock ─────────────────────────────────────────────────

    [RelayCommand]
    void ShowReduceStock()
    {
        ReduceQty = 0; ReduceNote = string.Empty; ReduceError = string.Empty;
        IsAddStockVisible = false;
        MaxAvailableText = $"Maximum available: {Supply?.QuantityInPieces} pcs";
        IsReduceStockVisible = true;
    }

    [RelayCommand]
    void HideReduceStock() => IsReduceStockVisible = false;

    [RelayCommand]
    void SelectNoteSuggestion(string suggestion) => ReduceNote = suggestion;

    [RelayCommand]
    async Task ConfirmReduceStockAsync()
    {
        ReduceError = string.Empty;
        if (ReduceQty <= 0) { ReduceError = "Enter a quantity greater than 0."; return; }
        if (Supply is not null && ReduceQty > Supply.QuantityInPieces)
        {
            ReduceError = $"Cannot reduce by more than current stock ({Supply.QuantityInPieces} pcs).";
            return;
        }
        if (IsBusy) return;
        IsBusy = true;
        try
        {
            await _db.ApplyStockChange(SupplyId, -ReduceQty, "Used", ReduceNote);
            IsReduceStockVisible = false;
            await LoadAsync();
        }
        finally { IsBusy = false; }
    }

    // ── Navigation ───────────────────────────────────────────────────

    [RelayCommand]
    async Task GoToEdit()
    {
        if (Supply is null) return;
        await Shell.Current.GoToAsync($"{nameof(AddSupplyPage)}?supplyId={Supply.Id}");
    }
}

public class StockLogRowViewModel
{
    public SupplyStockLog Log { get; }
    public StockLogRowViewModel(SupplyStockLog log) => Log = log;

    public string ChangeDisplay => Log.ChangeInPieces >= 0
        ? $"+{Log.ChangeInPieces} pcs" : $"{Log.ChangeInPieces} pcs";

    public string DateDisplay
    {
        get
        {
            if (DateTime.TryParse(Log.Timestamp, out var dt))
                return dt.ToString("MMM d");
            return Log.Timestamp;
        }
    }

    public string TypeDisplay => Log.ChangeType;
    public bool IsIncrease => Log.ChangeInPieces >= 0;
    public string NoteDisplay => string.IsNullOrWhiteSpace(Log.Note) ? "—" : Log.Note;
}
