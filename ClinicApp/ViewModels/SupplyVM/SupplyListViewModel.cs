using ClinicApp.Models;
using ClinicApp.Services;
using ClinicApp.Views.SupplyRelated;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;

namespace ClinicApp.ViewModels.SupplyVM;

public partial class SupplyListViewModel : ObservableObject
{
    private readonly DatabaseService _db;

    [ObservableProperty] private bool isBusy;
    [ObservableProperty] private bool isRefreshing;
    [ObservableProperty] private bool isEmpty;
    [ObservableProperty] private int lowStockCount;
    [ObservableProperty] private string lowStockSummary = string.Empty;
    [ObservableProperty] private bool hasLowStock;
    [ObservableProperty] private string searchText = string.Empty;

    public ObservableCollection<SupplyCardViewModel> AllCards { get; } = new();
    public ObservableCollection<SupplyCardViewModel> FilteredCards { get; } = new();

    public SupplyListViewModel(DatabaseService db) => _db = db;

    partial void OnSearchTextChanged(string value) => ApplyFilter();

    [RelayCommand]
    public async Task LoadSuppliesAsync()
    {
        if (IsBusy) return;
        IsBusy = true;
        try
        {
            var list = await _db.GetSupplyItems();
            AllCards.Clear();
            foreach (var s in list)
                AllCards.Add(new SupplyCardViewModel(s));
            ApplyFilter();
            RefreshSummary();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[LoadSupplies] {ex.Message}");
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void ApplyFilter()
    {
        FilteredCards.Clear();
        var q = SearchText?.Trim().ToLowerInvariant() ?? string.Empty;
        foreach (var card in AllCards)
        {
            if (string.IsNullOrEmpty(q) ||
                card.Supply.Name.ToLowerInvariant().Contains(q) ||
                card.Supply.Unit.ToLowerInvariant().Contains(q) ||
                card.Supply.SizeVariant.ToLowerInvariant().Contains(q))
            {
                FilteredCards.Add(card);
            }
        }
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
    async Task GoToAddSupply() =>
        await Shell.Current.GoToAsync(nameof(AddSupplyPage));

    [RelayCommand]
    async Task ViewSupplyInfo(SupplyCardViewModel card)
    {
        if (card is null) return;
        await Shell.Current.GoToAsync($"{nameof(SupplyInfoPage)}?supplyId={card.Supply.Id}");
    }

    [RelayCommand]
    async Task EditSupply(SupplyCardViewModel card)
    {
        if (card is null) return;
        await Shell.Current.GoToAsync($"{nameof(AddSupplyPage)}?supplyId={card.Supply.Id}");
    }

    [RelayCommand]
    async Task DeleteSupply(SupplyCardViewModel card)
    {
        if (card is null) return;

        bool ok = await Shell.Current.DisplayAlert(
            "Delete Supply",
            $"Delete \"{card.Supply.Name}\" and all its stock history?",
            "Delete", "Cancel");
        if (!ok) return;

        try
        {
            await _db.DeleteSupplyItem(card.Supply);
        }
        catch (Exception ex)
        {
            // Item may have already been deleted externally — log and continue
            System.Diagnostics.Debug.WriteLine($"[Delete] {ex.Message}");
        }

        // Always remove from UI regardless of DB result
        AllCards.Remove(card);
        ApplyFilter();
        RefreshSummary();
    }

    [RelayCommand]
    void ToggleCard(SupplyCardViewModel card)
    {
        if (card is null) return;
        card.IsExpanded = !card.IsExpanded;
    }
}
