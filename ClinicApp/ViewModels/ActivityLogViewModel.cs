using ClinicApp.Models.SupabaseModels;
using ClinicApp.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using System.Linq;

namespace ClinicApp.ViewModels;

// One filter chip: a display label plus the raw activity Types it matches (empty array = All).
public partial class ActivityFilterOption : ObservableObject
{
    public string Label { get; }
    public string[] Types { get; }

    [ObservableProperty] private bool isSelected;

    // Builds a filter option, optionally starting selected.
    public ActivityFilterOption(string label, string[] types, bool isSelected = false)
    {
        Label = label;
        Types = types;
        IsSelected = isSelected;
    }
}

// Backs the "View All" page opened from Home's Recent Activity card.
public partial class ActivityLogViewModel : ObservableObject
{
    readonly SupabaseDataService dataService;
    List<SupabaseActivityLog> allActivities = new();

    [ObservableProperty] private bool isBusy;
    [ObservableProperty] private bool isRefreshing;

    public ObservableCollection<SupabaseActivityLog> Activities { get; } = new();

    // Filter chips grouped by module rather than by raw action, so one chip covers a whole area of the app.
    public ObservableCollection<ActivityFilterOption> Filters { get; } = new()
    {
        new ActivityFilterOption("All", Array.Empty<string>(), isSelected: true),
        new ActivityFilterOption("Patients", new[] { "NewPatient", "PatientUpdated", "PatientDeleted" }),
        new ActivityFilterOption("Appointments", new[] { "NewBooking", "AppointmentCompleted", "AppointmentCancelled", "AppointmentRescheduled" }),
        new ActivityFilterOption("Payments", new[] { "Payment" }),
        new ActivityFilterOption("Supplies", new[] { "StockChange", "NewSupplyItem", "SupplyDeleted" }),
        new ActivityFilterOption("Services", new[] { "NewService", "ServicePriceChanged", "ServiceDeleted" }),
        new ActivityFilterOption("Users", new[] { "NewUser", "UserDeactivated", "UserDeleted" }),
    };

    // Injects the shared data service.
    public ActivityLogViewModel(SupabaseDataService dataService) => this.dataService = dataService;

    // Loads the full activity history, newest first, then applies the current filter.
    [RelayCommand]
    async Task Load()
    {
        if (IsBusy) return;
        IsBusy = true;
        try
        {
            allActivities = (await dataService.GetAllActivitiesAsync()).ToList();
            ApplyFilter();
        }
        finally { IsBusy = false; }
    }

    // Pull-to-refresh.
    [RelayCommand]
    async Task Refresh()
    {
        IsRefreshing = true;
        try { await Load(); }
        finally { IsRefreshing = false; }
    }

    // Marks the tapped chip as the only selected one and re-filters the visible list.
    [RelayCommand]
    void SelectFilter(ActivityFilterOption option)
    {
        foreach (var filter in Filters)
            filter.IsSelected = filter == option;
        ApplyFilter();
    }

    // Rebuilds the visible list from allActivities using the currently selected chip.
    void ApplyFilter()
    {
        var selected = Filters.FirstOrDefault(f => f.IsSelected) ?? Filters[0];
        var filtered = selected.Types.Length == 0
            ? allActivities
            : allActivities.Where(a => selected.Types.Contains(a.Type));

        Activities.Clear();
        foreach (var activity in filtered)
            Activities.Add(activity);
    }
}
