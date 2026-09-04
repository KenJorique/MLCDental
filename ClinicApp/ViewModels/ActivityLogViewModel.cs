using ClinicApp.Models.SupabaseModels;
using ClinicApp.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;

namespace ClinicApp.ViewModels;

// Backs the "View All" page opened from Home's Recent Activity card.
public partial class ActivityLogViewModel : ObservableObject
{
    readonly SupabaseDataService dataService;

    [ObservableProperty] private bool isBusy;
    [ObservableProperty] private bool isRefreshing;

    public ObservableCollection<SupabaseActivityLog> Activities { get; } = new();

    // Injects the shared data service.
    public ActivityLogViewModel(SupabaseDataService dataService) => this.dataService = dataService;

    // Loads the full activity history, newest first.
    [RelayCommand]
    async Task Load()
    {
        if (IsBusy) return;
        IsBusy = true;
        try
        {
            var all = await dataService.GetAllActivitiesAsync();
            Activities.Clear();
            foreach (var activity in all)
                Activities.Add(activity);
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
}
