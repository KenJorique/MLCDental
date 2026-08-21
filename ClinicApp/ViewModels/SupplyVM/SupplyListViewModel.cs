using ClinicApp.Models;
using ClinicApp.Services;
using ClinicApp.Views.Shared;
using ClinicApp.Views.SupplyRelated;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;

namespace ClinicApp.ViewModels.SupplyVM;

public partial class SupplyListViewModel : ObservableObject
{
    private readonly SupabaseDataService _supabase;

    [ObservableProperty] private bool isBusy;
    [ObservableProperty] private bool isRefreshing;
    [ObservableProperty] private bool isEmpty;
    [ObservableProperty] private int lowStockCount;
    [ObservableProperty] private string lowStockSummary = string.Empty;
    [ObservableProperty] private bool hasLowStock;
    [ObservableProperty] private string searchText = string.Empty;

    // Renamed from CurrentSort: this drives the All / Low Stock / Out of
    // Stock filter PILLS. It was previously also being set by the sort
    // action sheet — same property doing two unrelated jobs, which
    // meant the pills and the sort button silently fought over the same
    // state. CurrentSortOption below is the real sort choice now.
    [ObservableProperty] private string currentFilter = "All";

    // Actual sort order, set via the sort button's action sheet. Default
    // matches the requested "out of stock, then low stock, then in
    // stock, alphabetical within each" ordering — see ApplySort below.
    [ObservableProperty] private string currentSortOption = "Default";

    // ── Filter pill counts ──────────────────────────────────────────
    // AllCount / OutOfStockCount are straightforward totals. LowStockOnlyCount
    // is separate from the existing LowStockCount above: LowStockCount
    // feeds the "N items are low or out of stock" banner and intentionally
    // includes out-of-stock items too, but the "Low Stock" filter case
    // below explicitly excludes out-of-stock items (c.IsLowStock &&
    // !c.IsOutOfStock) so the Low Stock and Out of Stock pills don't
    // double-count the same item. The pill's count needs to match what
    // the filter actually returns, so it uses that same exclusion.
    [ObservableProperty] private int allCount;
    [ObservableProperty] private int lowStockOnlyCount;
    [ObservableProperty] private int outOfStockCount;

    public ObservableCollection<SupplyCardViewModel> AllCards { get; } = new();
    public ObservableCollection<SupplyCardViewModel> FilteredCards { get; } = new();

    // Same pattern as Balance Management's EmptyStateTitle/EmptyStateMessage:
    // search takes priority over the filter (since a search with zero
    // matches is its own distinct situation, whichever pill is active),
    // and "Tap + to add your first supply item" only shows for the true
    // empty-list case ("All", no search), not for an empty filtered view.
    public string EmptyStateTitle
    {
        get
        {
            if (!string.IsNullOrWhiteSpace(SearchText)) return "No matches";
            return CurrentFilter switch
            {
                "Low Stock" => "No low stock supplies",
                "Out of Stock" => "No out of stock supplies",
                _ => "No supplies found"
            };
        }
    }

    public string EmptyStateMessage
    {
        get
        {
            if (!string.IsNullOrWhiteSpace(SearchText))
                return $"No supplies match \"{SearchText.Trim()}\".";
            return CurrentFilter switch
            {
                "Low Stock" => "No supplies are currently low in stock.",
                "Out of Stock" => "No supplies are currently out of stock.",
                _ => "Tap + to add your first supply item."
            };
        }
    }

    public SupplyListViewModel(SupabaseDataService supabase) => _supabase = supabase;

    partial void OnSearchTextChanged(string value)
    {
        ApplyFilterAndSort();
        OnPropertyChanged(nameof(EmptyStateTitle));
        OnPropertyChanged(nameof(EmptyStateMessage));
    }

    partial void OnCurrentFilterChanged(string value)
    {
        ApplyFilterAndSort();
        OnPropertyChanged(nameof(EmptyStateTitle));
        OnPropertyChanged(nameof(EmptyStateMessage));
    }

    partial void OnCurrentSortOptionChanged(string value) => ApplyFilterAndSort();

    // Pills call this directly — sets the same CurrentFilter that used
    // to be driven only by the (now removed) stock-status action sheet.
    [RelayCommand]
    void SetFilter(string mode) => CurrentFilter = mode;

    [RelayCommand]
    public async Task LoadSuppliesAsync()
    {
        if (IsBusy) return;
        IsBusy = true;
        try
        {
            var list = await _supabase.GetSuppliesAsync();
            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                AllCards.Clear();
                foreach (var s in list)
                    AllCards.Add(new SupplyCardViewModel(s));
                ApplyFilterAndSort();
                RefreshSummary();
            });
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[LoadSupplies] {ex.Message}");
        }
        finally { IsBusy = false; }
    }

    private void ApplyFilterAndSort()
    {
        var q = SearchText?.Trim().ToLowerInvariant() ?? string.Empty;

        var source = AllCards.AsEnumerable();

        if (!string.IsNullOrEmpty(q))
            source = source.Where(c => c.Supply.Name.ToLowerInvariant().Contains(q));

        source = CurrentFilter switch
        {
            "Low Stock" => source.Where(c => c.IsLowStock && !c.IsOutOfStock),
            "Out of Stock" => source.Where(c => c.IsOutOfStock),
            _ => source
        };

        source = ApplySort(source);

        FilteredCards.Clear();
        foreach (var card in source)
            FilteredCards.Add(card);

        IsEmpty = FilteredCards.Count == 0;
    }

    // "Default" matches the requested behavior: out of stock first, then
    // low stock, then in stock, alphabetical within each group.
    //
    // NOTE — Recently Updated assumes SupabaseSupplyItem has an
    // UpdatedAt property. I don't have that model file; if the real
    // field is named differently (LastModified, ModifiedAt, etc.),
    // this is the one line to fix.
    private IEnumerable<SupplyCardViewModel> ApplySort(IEnumerable<SupplyCardViewModel> source) =>
        CurrentSortOption switch
        {
            "Recently Updated" => source.OrderByDescending(c => c.Supply.UpdatedAt),
            "Name (A-Z)" => source.OrderBy(c => c.Supply.Name, StringComparer.OrdinalIgnoreCase),
            "Name (Z-A)" => source.OrderByDescending(c => c.Supply.Name, StringComparer.OrdinalIgnoreCase),
            "Stock: Low to High" => source.OrderBy(c => c.Supply.QuantityInPieces),
            "Stock: High to Low" => source.OrderByDescending(c => c.Supply.QuantityInPieces),
            _ => source.OrderBy(StatusPriority).ThenBy(c => c.Supply.Name, StringComparer.OrdinalIgnoreCase)
        };

    private static int StatusPriority(SupplyCardViewModel c) =>
        c.IsOutOfStock ? 0 : c.IsLowStock ? 1 : 2;

    private void RefreshSummary()
    {
        LowStockCount = AllCards.Count(c => c.IsLowStock);
        HasLowStock = LowStockCount > 0;
        LowStockSummary = LowStockCount == 0 ? string.Empty
            : LowStockCount == 1 ? "1 item is low or out of stock"
            : $"{LowStockCount} items are low or out of stock";

        AllCount = AllCards.Count;
        OutOfStockCount = AllCards.Count(c => c.IsOutOfStock);
        LowStockOnlyCount = AllCards.Count(c => c.IsLowStock && !c.IsOutOfStock);
    }

    [RelayCommand]
    async Task ShowSortOptions()
    {
        var result = await Shell.Current.DisplayActionSheet(
            "Sort By", "Cancel", null,
            "Recently Updated", "Name (A-Z)", "Name (Z-A)",
            "Stock: Low to High", "Stock: High to Low");

        if (result is null || result == "Cancel") return;
        CurrentSortOption = result;
    }

    [RelayCommand]
    async Task GoToAddSupply() =>
        await Shell.Current.GoToAsync(nameof(AddSupplyPage));

    [RelayCommand]
    async Task ViewSupplyInfo(SupplyCardViewModel card)
    {
        if (card is null) return;
        await Shell.Current.GoToAsync($"{nameof(SupplyInfoPage)}?supplyId={card.Supply.Id}");
    }

    [RelayCommand]
    async Task Refresh()
    {
        IsRefreshing = true;
        try { await LoadSuppliesAsync(); }
        finally { IsRefreshing = false; }
    }

    [RelayCommand]
    async Task QuickAddStock(SupplyCardViewModel card)
    {
        if (card is null) return;
        await Shell.Current.GoToAsync(
            $"{nameof(AddStockPage)}?supplyId={card.Supply.Id}&hasExpiration={card.Supply.HasExpiration}");
    }

    [RelayCommand]
    async Task QuickReduceStock(SupplyCardViewModel card)
    {
        if (card is null) return;

        if (card.Supply.QuantityInPieces <= 0)
        {
            await Shell.Current.DisplayAlert("No Stock",
                $"\"{card.Supply.Name}\" has no stock to reduce.", "OK");
            return;
        }

        await Shell.Current.GoToAsync(
            $"{nameof(ReduceStockPage)}?supplyId={card.Supply.Id}&currentStock={card.Supply.QuantityInPieces}");
    }
    [RelayCommand]
    async Task ShowActionSheet(SupplyCardViewModel card)
    {
        if (card is null) return;

        var sheet = new ItemActionSheet();
        sheet.Configure(
            title: card.Supply.Name,
            subtitle: $"Currently {card.Supply.QuantityDisplay}",
            options: new[]
            {
            new ActionSheetOption
            {
                Icon = "\ue145",  // add
                Label = "Add Stock",
                Subtitle = "Restock this item",
                IconBackgroundColor = Color.FromArgb("#E8F5E9"),
                IconColor = Color.FromArgb("#2E7D32"),
                OnTapped = async () => await QuickAddStock(card),
            },
            new ActionSheetOption
            {
                Icon = "\ue15b",  // remove
                Label = "Reduce Stock",
                Subtitle = "Log usage, damage, or expiry",
                IconBackgroundColor = Color.FromArgb("#FFF3E0"),
                IconColor = Color.FromArgb("#E65100"),
                OnTapped = async () => await QuickReduceStock(card),
            },
            new ActionSheetOption
            {
                Icon = "\ue88e",
                Label = "View Info",
                Subtitle = "See full supply details & history",
                IconBackgroundColor = Color.FromArgb("#E3F2FD"),
                IconColor = Color.FromArgb("#1565C0"),
                OnTapped = async () =>
                    await Shell.Current.GoToAsync($"{nameof(SupplyInfoPage)}?supplyId={card.Supply.Id}"),
            },
            new ActionSheetOption
            {
                Icon = "\ue3c9",
                Label = "Edit Details",
                Subtitle = "Name, unit, minimum stock — not quantity",
                IconBackgroundColor = Color.FromArgb("#F3E5F5"),
                IconColor = Color.FromArgb("#6A1B9A"),
                OnTapped = async () =>
                    await Shell.Current.GoToAsync($"{nameof(AddSupplyPage)}?supplyId={card.Supply.Id}"),
            },
            new ActionSheetOption
            {
                Icon = "\ue872",
                Label = "Delete",
                Subtitle = "Hide from supply list",
                LabelColor = Colors.Crimson,
                IconBackgroundColor = Color.FromArgb("#FFEBEE"),
                OnTapped = async () => await DeleteSupplyAsync(card),
            },
            });

        await sheet.ShowAsync();
    }

    private async Task DeleteSupplyAsync(SupplyCardViewModel card)
    {
        bool ok = await Shell.Current.DisplayAlert(
            "Remove Supply",
            $"Remove \"{card.Supply.Name}\" from the supply list?",
            "Remove", "Cancel");
        if (!ok) return;

        IsBusy = true;
        try
        {
            var success = await _supabase.DeleteSupplyAsync(card.Supply.Id);
            if (!success)
            {
                await Shell.Current.DisplayAlert("Error", "Could not delete item. Try again.", "OK");
                return;
            }

            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                var inAll = AllCards.FirstOrDefault(c => c.Supply.Id == card.Supply.Id);
                var inFiltered = FilteredCards.FirstOrDefault(c => c.Supply.Id == card.Supply.Id);
                if (inAll is not null) AllCards.Remove(inAll);
                if (inFiltered is not null) FilteredCards.Remove(inFiltered);
                RefreshSummary();
                IsEmpty = FilteredCards.Count == 0;
            });
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[DeleteSupply] {ex.Message}");
            await Shell.Current.DisplayAlert("Error", $"Could not delete item: {ex.Message}", "OK");
        }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    void ToggleCard(SupplyCardViewModel card)
    {
        if (card is null) return;
        card.IsExpanded = !card.IsExpanded;
    }
}