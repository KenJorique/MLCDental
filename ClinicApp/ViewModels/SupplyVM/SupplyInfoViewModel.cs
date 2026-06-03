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

    // Computed display
    public string StockDisplay => Supply?.QuantityDisplay ?? "—";
    public bool IsLowStock => Supply?.IsLowStock ?? false;
    public bool IsOutOfStock => Supply?.IsOutOfStock ?? false;
    public string StockStatus => IsOutOfStock ? "Out of Stock" : IsLowStock ? "Low Stock" : "In Stock";
    public string StockStatusColor => IsOutOfStock ? "#D32F2F" : IsLowStock ? "#F57C00" : "#388E3C";
    /// Strips "Per " prefix — shows "Piece", "Box", "Pack", "Kit"
    public string UnitDisplay => Supply?.Unit?.Replace("Per ", string.Empty) ?? "—";

    public string ExpirationDisplay => Supply is null ? "—"
        : Supply.HasExpiration && !string.IsNullOrWhiteSpace(Supply.ExpirationDate)
            ? Supply.ExpirationDate : "—";

    // 4 most recent logs shown on SupplyInfoPage
    public IEnumerable<StockLogRowViewModel> RecentLogs => Logs.Take(4);

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

            OnPropertyChanged(nameof(RecentLogs));
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
        OnPropertyChanged(nameof(StockStatusColor));
        OnPropertyChanged(nameof(UnitDisplay));
        OnPropertyChanged(nameof(ExpirationDisplay));
    }

    // ── Navigation ───────────────────────────────────────────────────

    [RelayCommand]
    public async Task GoToAddStock()
    {
        if (Supply is null) return;
        await Shell.Current.GoToAsync(
            $"{nameof(AddStockPage)}?supplyId={Supply.Id}&hasExpiration={Supply.HasExpiration}");
    }

    [RelayCommand]
    public async Task GoToReduceStock()
    {
        if (Supply is null) return;
        await Shell.Current.GoToAsync(
            $"{nameof(ReduceStockPage)}?supplyId={Supply.Id}&currentStock={Supply.QuantityInPieces}");
    }

    [RelayCommand]
    async Task ViewAllLogs()
    {
        if (Supply is null) return;
        await Shell.Current.GoToAsync(
            $"{nameof(StockHistoryPage)}?supplyId={Supply.Id}");
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
