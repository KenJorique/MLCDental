using ClinicApp.Models;
using ClinicApp.Services;
using ClinicApp.Views.Shared;
using ClinicApp.Views.SupplyRelated;
using CommunityToolkit.Maui.Views;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;

namespace ClinicApp.ViewModels.SupplyVM;

// Lets other pages open this pre-filtered via ?filter=Low Stock / ?filter=Out of Stock.
[QueryProperty(nameof(CurrentFilter), "filter")]
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
    [ObservableProperty] private string currentFilter = "All";
    [ObservableProperty] private string currentSortOption = "Default";

    // Filter pill counts.
    [ObservableProperty] private int allCount;
    [ObservableProperty] private int lowStockOnlyCount;
    [ObservableProperty] private int outOfStockCount;

    public ObservableCollection<SupplyCardViewModel> AllCards { get; } = new();
    public ObservableCollection<SupplyCardViewModel> FilteredCards { get; } = new();

    // Empty-state title, varies by filter/search.
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

    // Empty-state subtitle, varies by filter/search.
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

    // Injects the shared data service.
    public SupplyListViewModel(SupabaseDataService supabase) => _supabase = supabase;

    // Re-filters as the user types.
    partial void OnSearchTextChanged(string value)
    {
        ApplyFilterAndSort();
        OnPropertyChanged(nameof(EmptyStateTitle));
        OnPropertyChanged(nameof(EmptyStateMessage));
    }

    // Re-filters when the filter pill or the incoming ?filter= query changes.
    partial void OnCurrentFilterChanged(string value)
    {
        ApplyFilterAndSort();
        OnPropertyChanged(nameof(EmptyStateTitle));
        OnPropertyChanged(nameof(EmptyStateMessage));
    }

    // Re-sorts when the sort option changes.
    partial void OnCurrentSortOptionChanged(string value) => ApplyFilterAndSort();

    // Sets the active filter (used by the filter pills).
    [RelayCommand]
    void SetFilter(string mode) => CurrentFilter = mode;

    // Loads all supplies and rebuilds the filtered/sorted list.
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

    // Applies search + filter + sort, then rebuilds FilteredCards.
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

    // Sorts by status priority by default, or by the picked sort option.
    private IEnumerable<SupplyCardViewModel> ApplySort(IEnumerable<SupplyCardViewModel> source) =>
        CurrentSortOption switch
        {
            "Ascending" => source.OrderBy(c => c.Supply.Name, StringComparer.OrdinalIgnoreCase),
            "Descending" => source.OrderByDescending(c => c.Supply.Name, StringComparer.OrdinalIgnoreCase),
            "Stock: Low to High" => source.OrderBy(c => c.Supply.QuantityInPieces),
            "Stock: High to Low" => source.OrderByDescending(c => c.Supply.QuantityInPieces),
            _ => source.OrderBy(StatusPriority).ThenBy(c => c.Supply.Name, StringComparer.OrdinalIgnoreCase)
        };

    // Out of stock first, then low stock, then everything else.
    private static int StatusPriority(SupplyCardViewModel c) =>
        c.IsOutOfStock ? 0 : c.IsLowStock ? 1 : 2;

    // Recomputes the filter-pill counts and low-stock banner text.
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

    // Shows the sort-options action sheet and applies the pick.
    [RelayCommand]
    async Task ShowSortOptions()
    {
        var result = await Shell.Current.DisplayActionSheet(
            "Sort By", "Cancel", null,
            "Ascending", "Descending",
            "Stock: Low to High", "Stock: High to Low");

        if (result is null || result == "Cancel") return;
        CurrentSortOption = result;
    }

    // Opens the Add Supply page.
    [RelayCommand]
    async Task GoToAddSupply() =>
        await Shell.Current.GoToAsync(nameof(AddSupplyPage));

    // Opens the tapped item's info page.
    [RelayCommand]
    async Task ViewSupplyInfo(SupplyCardViewModel card)
    {
        if (card is null) return;
        await Shell.Current.GoToAsync($"{nameof(SupplyInfoPage)}?supplyId={card.Supply.Id}");
    }

    // Pull-to-refresh.
    [RelayCommand]
    async Task Refresh()
    {
        IsRefreshing = true;
        try { await LoadSuppliesAsync(); }
        finally { IsRefreshing = false; }
    }

    // Opens Add Stock for the tapped item.
    [RelayCommand]
    async Task QuickAddStock(SupplyCardViewModel card)
    {
        if (card is null) return;
        await Shell.Current.GoToAsync(
            $"{nameof(AddStockPage)}?supplyId={card.Supply.Id}&hasExpiration={card.Supply.HasExpiration}");
    }

    // Opens Reduce Stock for the tapped item, if it has any stock left.
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

    // Shows the per-item action sheet (add/reduce stock, view info, edit, delete).
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

    // Confirms with the popup, then deletes the item, logs it, and removes it from both card lists.
    private async Task DeleteSupplyAsync(SupplyCardViewModel card)
    {
        var popup = new ConfirmationPopup(
            "Remove Supply?",
            $"Remove \"{card.Supply.Name}\" from the supply list?",
            confirmText: "Remove");

        var result = await Shell.Current.ShowPopupAsync(popup);
        if (result is not bool confirmed || !confirmed) return;

        IsBusy = true;
        try
        {
            var success = await _supabase.DeleteSupplyAsync(card.Supply.Id);
            if (!success)
            {
                await Shell.Current.DisplayAlert("Error", "Could not delete item. Try again.", "OK");
                return;
            }

            await _supabase.LogActivityAsync("SupplyDeleted", $"{card.Supply.Name} was deleted");

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

    // Expands/collapses the tapped card.
    [RelayCommand]
    void ToggleCard(SupplyCardViewModel card)
    {
        if (card is null) return;
        card.IsExpanded = !card.IsExpanded;
    }
}
