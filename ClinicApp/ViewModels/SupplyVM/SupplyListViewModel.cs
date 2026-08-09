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
    [ObservableProperty] private string currentSort = "All";

    public ObservableCollection<SupplyCardViewModel> AllCards { get; } = new();
    public ObservableCollection<SupplyCardViewModel> FilteredCards { get; } = new();

    public SupplyListViewModel(SupabaseDataService supabase) => _supabase = supabase;

    partial void OnSearchTextChanged(string value) => ApplyFilter();
    partial void OnCurrentSortChanged(string value) => ApplyFilter();

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
                ApplyFilter();
                RefreshSummary();
            });
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[LoadSupplies] {ex.Message}");
        }
        finally { IsBusy = false; }
    }

    private void ApplyFilter()
    {
        FilteredCards.Clear();
        var q = SearchText?.Trim().ToLowerInvariant() ?? string.Empty;

        var source = AllCards.AsEnumerable();

        if (!string.IsNullOrEmpty(q))
            source = source.Where(c => c.Supply.Name.ToLowerInvariant().Contains(q));

        source = CurrentSort switch
        {
            "Low Stock" => source.Where(c => c.IsLowStock && !c.IsOutOfStock),
            "Out of Stock" => source.Where(c => c.IsOutOfStock),
            _ => source
        };

        foreach (var card in source)
            FilteredCards.Add(card);

        IsEmpty = FilteredCards.Count == 0;
    }

    private void RefreshSummary()
    {
        LowStockCount = AllCards.Count(c => c.IsLowStock);
        HasLowStock = LowStockCount > 0;
        LowStockSummary = LowStockCount == 0 ? string.Empty
            : LowStockCount == 1 ? "1 item is low or out of stock"
            : $"{LowStockCount} items are low or out of stock";
    }

    [RelayCommand]
    async Task ShowSortOptions()
    {
        var result = await Shell.Current.DisplayActionSheet(
            "Filter by Stock Status", "Cancel", null,
            "All", "Low Stock", "Out of Stock");

        if (result is null || result == "Cancel") return;
        CurrentSort = result;
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