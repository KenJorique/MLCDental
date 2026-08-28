using ClinicApp.Models;
using ClinicApp.Services;
using ClinicApp.Views.Shared;
using ClinicApp.Views.ServicesRelated;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;

namespace ClinicApp.ViewModels.ServicesRelatedVM;

public partial class ServiceViewModel : ObservableObject
{
    readonly SupabaseDataService _supabase;

    [ObservableProperty] private bool isBusy;
    [ObservableProperty] private bool isRefreshing;
    [ObservableProperty] private string searchText = string.Empty;

    // "Name" | "PriceLowHigh" | "PriceHighLow"
    [ObservableProperty] private string currentSort = "Name";

    [ObservableProperty] private string emptyStateTitle = "No services yet";
    [ObservableProperty] private string emptyStateMessage = "Tap \"+ Add Service\" to create your first one.";

    // Full unfiltered set, populated from Supabase
    public ObservableCollection<ServiceCardViewModel> ServiceCards { get; set; } = new();

    // What the CollectionView actually binds to — filtered + sorted view of ServiceCards
    public ObservableCollection<ServiceCardViewModel> FilteredCards { get; set; } = new();

    // Injects the Supabase data service used for reading/writing services.
    public ServiceViewModel(SupabaseDataService supabase) => _supabase = supabase;

    // Re-filters/sorts the list whenever the search text changes.
    partial void OnSearchTextChanged(string value) => ApplyFilterAndSort();

    // Re-filters/sorts the list whenever the sort option changes.
    partial void OnCurrentSortChanged(string value) => ApplyFilterAndSort();

    // Loads (or reloads) the service list from Supabase.
    [RelayCommand]
    public async Task LoadServices()
    {
        IsBusy = true;
        try
        {
            var serviceList = await _supabase.GetServicesAsync();
            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                ServiceCards.Clear();
                foreach (var s in serviceList)
                    ServiceCards.Add(new ServiceCardViewModel(s));

                ApplyFilterAndSort();
            });
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[Supabase] LoadServices: {ex.Message}");
        }
        finally
        {
            IsBusy = false;
            IsRefreshing = false;
        }
    }

    // Recomputes FilteredCards from ServiceCards based on SearchText + CurrentSort.
    private void ApplyFilterAndSort()
    {
        IEnumerable<ServiceCardViewModel> query = ServiceCards;

        if (!string.IsNullOrWhiteSpace(SearchText))
        {
            var term = SearchText.Trim();
            query = query.Where(c =>
                !string.IsNullOrEmpty(c.ServiceName) &&
                c.ServiceName.Contains(term, StringComparison.OrdinalIgnoreCase));
        }

        query = CurrentSort switch
        {
            "PriceLowHigh" => query.OrderBy(c => c.Service.BasePrice),
            "PriceHighLow" => query.OrderByDescending(c => c.Service.BasePrice),
            _ => query.OrderBy(c => c.ServiceName, StringComparer.OrdinalIgnoreCase),
        };

        FilteredCards.Clear();
        foreach (var c in query)
            FilteredCards.Add(c);

        if (ServiceCards.Count == 0)
        {
            EmptyStateTitle = "No services yet";
            EmptyStateMessage = "Tap \"+ Add Service\" to create your first one.";
        }
        else if (FilteredCards.Count == 0)
        {
            EmptyStateTitle = "No matches found";
            EmptyStateMessage = $"Nothing matches \"{SearchText}\".";
        }
    }

    // Sort icon → shows the action sheet with the 3 sort options.
    [RelayCommand]
    async Task ShowSortOptions()
    {
        string action = await Shell.Current.DisplayActionSheet(
            "Sort by", "Cancel", null,
            "Name (A–Z)", "Price: Low to High", "Price: High to Low");

        CurrentSort = action switch
        {
            "Name (A–Z)" => "Name",
            "Price: Low to High" => "PriceLowHigh",
            "Price: High to Low" => "PriceHighLow",
            _ => CurrentSort, // "Cancel" or dismissed — leave sort unchanged
        };
    }

    // Opens the Edit/Delete action sheet for a tapped service card.
    [RelayCommand]
    async Task ShowActionSheet(ServiceCardViewModel card)
    {
        if (card is null) return;

        var sheet = new ItemActionSheet();
        sheet.Configure(
            title: card.Service.Name,
            subtitle: string.IsNullOrWhiteSpace(card.Service.Description)
                ? string.Empty
                : card.Service.Description,
            options: new[]
            {
                new ActionSheetOption
                {
                    Icon = "\ue3c9",  // edit
                    Label = "Edit",
                    Subtitle = "Update service details",
                    IconBackgroundColor = Color.FromArgb("#E8F5E9"),
                    IconColor = Color.FromArgb("#2E7D32"),
                    OnTapped = async () =>
                        await Shell.Current.GoToAsync(
                            $"{nameof(AddServicePage)}?ServiceId={card.Service.Id}"),
                },
                new ActionSheetOption
                {
                    Icon = "\ue872",  // delete
                    Label = "Delete",
                    Subtitle = "Remove this service",
                    LabelColor = Colors.Crimson,
                    IconBackgroundColor = Color.FromArgb("#FFEBEE"),
                    IconColor = Colors.Crimson,
                    OnTapped = async () => await DeleteServiceAsync(card),
                },
            });

        await sheet.ShowAsync();
    }

    // Confirms with the user, then deletes the service and removes it from both lists.
    private async Task DeleteServiceAsync(ServiceCardViewModel card)
    {
        bool answer = await Shell.Current.DisplayAlert(
            "Delete Service",
            $"Are you sure you want to delete \"{card.Service.Name}\"?",
            "Delete", "Cancel");

        if (!answer) return;

        try
        {
            var success = await _supabase.DeleteServiceAsync(card.Service.Id);
            if (success)
            {
                var existing = ServiceCards.FirstOrDefault(c => c.Service.Id == card.Service.Id);
                if (existing is not null)
                    ServiceCards.Remove(existing);

                var existingFiltered = FilteredCards.FirstOrDefault(c => c.Service.Id == card.Service.Id);
                if (existingFiltered is not null)
                    FilteredCards.Remove(existingFiltered);
            }
            else
            {
                await Shell.Current.DisplayAlert("Error", "Could not delete the service. Try again.", "OK");
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[Delete] {ex.Message}");
        }
    }

    // Navigates to the Add Service form.
    [RelayCommand]
    async Task GoToAddService() =>
        await Shell.Current.GoToAsync(nameof(AddServicePage));
}
