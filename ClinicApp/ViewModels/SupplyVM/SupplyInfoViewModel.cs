using ClinicApp.Models.SupabaseModels;
using ClinicApp.Services;
using ClinicApp.Views.SupplyRelated;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;

namespace ClinicApp.ViewModels.SupplyVM;

[QueryProperty(nameof(SupplyId), "supplyId")]
public partial class SupplyInfoViewModel : ObservableObject
{
    private readonly SupabaseDataService _supabase;

    [ObservableProperty] private string supplyId = string.Empty;
    [ObservableProperty] private bool isBusy;
    [ObservableProperty] private SupabaseSupplyItem? supply;

    public ObservableCollection<StockLogRowViewModel> Logs { get; } = new();

    public string StockDisplay => Supply?.QuantityDisplay ?? "—";
    public bool IsLowStock => Supply?.IsLowStock ?? false;
    public bool IsOutOfStock => Supply?.IsOutOfStock ?? false;
    public string StockStatus => IsOutOfStock ? "Out of Stock" : IsLowStock ? "Low Stock" : "In Stock";

    // Pale badge background, matching SupplyCardViewModel/BillCardItem's status-pill convention.
    public string StockStatusColor => IsOutOfStock ? "#FCEAEA" : IsLowStock ? "#FFF3E0" : "#E8F5E9";

    // Saturated badge text color, paired with StockStatusColor above.
    public string StockStatusTextColor => IsOutOfStock ? "#C62828" : IsLowStock ? "#E65100" : "#2E7D32";

    public string UnitDisplay => Supply?.Unit?.Replace("Per ", string.Empty) ?? "—";

    // Shrinks the header name's font as it gets longer, so it stays on one line next to the badge and qty.
    public double NameFontSize => (Supply?.Name?.Length ?? 0) switch
    {
        <= 14 => 18,
        <= 20 => 16,
        <= 26 => 14,
        <= 32 => 12,
        _ => 11
    };

    public string ExpirationDisplay => Supply is null ? "—"
        : Supply.HasExpiration && !string.IsNullOrWhiteSpace(Supply.ExpirationDateDisplay)
            ? Supply.ExpirationDateDisplay : "—";

    public IEnumerable<StockLogRowViewModel> RecentLogs => Logs.Take(4);

    // Injects the shared data service.
    public SupplyInfoViewModel(SupabaseDataService supabase) => _supabase = supabase;

    // Loads the supply's details once SupplyId is set via navigation.
    partial void OnSupplyIdChanged(string value)
    {
        if (!string.IsNullOrWhiteSpace(value))
            MainThread.BeginInvokeOnMainThread(async () => await LoadAsync());
    }

    // Loads the supply item and its recent stock logs.
    [RelayCommand]
    public async Task LoadAsync()
    {
        if (string.IsNullOrWhiteSpace(SupplyId) || IsBusy) return;
        IsBusy = true;
        try
        {
            Supply = await _supabase.GetSupplyByIdAsync(SupplyId);
            if (Supply is null) return;
            NotifyDisplayChanged();

            var logs = await _supabase.GetLogsForSupplyAsync(SupplyId);
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

    // Raises change notifications for all computed display properties after a reload.
    private void NotifyDisplayChanged()
    {
        OnPropertyChanged(nameof(StockDisplay));
        OnPropertyChanged(nameof(IsLowStock));
        OnPropertyChanged(nameof(IsOutOfStock));
        OnPropertyChanged(nameof(StockStatus));
        OnPropertyChanged(nameof(StockStatusColor));
        OnPropertyChanged(nameof(StockStatusTextColor));
        OnPropertyChanged(nameof(UnitDisplay));
        OnPropertyChanged(nameof(NameFontSize));
        OnPropertyChanged(nameof(ExpirationDisplay));
    }

    // Opens Add Stock for this item.
    [RelayCommand]
    public async Task GoToAddStock()
    {
        if (Supply is null) return;
        await Shell.Current.GoToAsync(
            $"{nameof(AddStockPage)}?supplyId={Supply.Id}&hasExpiration={Supply.HasExpiration}");
    }

    // Opens Reduce Stock for this item.
    [RelayCommand]
    public async Task GoToReduceStock()
    {
        if (Supply is null) return;
        await Shell.Current.GoToAsync(
            $"{nameof(ReduceStockPage)}?supplyId={Supply.Id}&currentStock={Supply.QuantityInPieces}");
    }

    // Opens the full stock history for this item.
    [RelayCommand]
    async Task ViewAllLogs()
    {
        if (Supply is null) return;
        await Shell.Current.GoToAsync(
            $"{nameof(StockHistoryPage)}?supplyId={Supply.Id}");
    }
}

// Wraps a single stock log entry for display.
public class StockLogRowViewModel
{
    public SupabaseStockLog Log { get; }
    public StockLogRowViewModel(SupabaseStockLog log) => Log = log;

    public string ChangeDisplay => Log.ChangeInPieces >= 0
        ? $"+{Log.ChangeInPieces} pcs" : $"{Log.ChangeInPieces} pcs";

    public string DateDisplay => Log.CreatedAt.ToString("MMM d");

    public string TypeDisplay => Log.ChangeType;
    public bool IsIncrease => Log.ChangeInPieces >= 0;
    public string NoteDisplay => string.IsNullOrWhiteSpace(Log.Note) ? "—" : Log.Note;
}
