using ClinicApp.Models.PatientModels;
using ClinicApp.Models.SupabaseModels;
using ClinicApp.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace ClinicApp.ViewModels.ServicesRelatedVM;

[QueryProperty(nameof(ServiceId), "ServiceId")]
public partial class AddServiceViewModel : ObservableObject
{
    readonly SupabaseDataService _supabase;

    public AddServiceViewModel(SupabaseDataService supabase) => _supabase = supabase;

    [ObservableProperty] string pageTitle = "Add Service";
    [ObservableProperty] string? serviceId;
    [ObservableProperty] string? serviceName;
    [ObservableProperty] decimal servicePrice;
    [ObservableProperty] string? serviceDescription;

    // ── Multi-session configuration ──
    [ObservableProperty] bool requiresMultipleSessions;
    [ObservableProperty] int totalSessions = 2;
    [ObservableProperty] int followupIntervalDays = 14;

    partial void OnRequiresMultipleSessionsChanged(bool value)
    {
        OnPropertyChanged(nameof(ShowSessionFields));
    }

    public bool ShowSessionFields => RequiresMultipleSessions;

    partial void OnServiceIdChanged(string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            PageTitle = "Edit Service";
            _ = LoadServiceDataAsync(value);
        }
    }

    private async Task LoadServiceDataAsync(string id)
    {
        var list = await _supabase.GetServicesAsync();
        var service = list.FirstOrDefault(s => s.Id == id);
        if (service != null)
        {
            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                ServiceName = service.Name;
                ServicePrice = service.BasePrice;
                ServiceDescription = service.Description;
                RequiresMultipleSessions = service.RequiresMultipleSessions;
                TotalSessions = service.DefaultTotalSessions ?? 2;
                FollowupIntervalDays = service.FollowupIntervalDays ?? 14;
                OnPropertyChanged(nameof(ShowSessionFields));
            });
        }
    }

    // ─── Save command ────────────────────────────────────────

    [RelayCommand]
    async Task Save()
    {
        if (string.IsNullOrWhiteSpace(ServiceName))
        {
            await Shell.Current.DisplayAlert("Validation", "Service name is required.", "OK");
            return;
        }
        if (ServicePrice <= 0)
        {
            await Shell.Current.DisplayAlert("Validation", "Please enter a valid price.", "OK");
            return;
        }
        if (RequiresMultipleSessions && TotalSessions < 2)
        {
            await Shell.Current.DisplayAlert("Validation", "Multi-session services need at least 2 sessions.", "OK");
            return;
        }

        // Recurring/open-ended services (e.g. Braces Adjustment) can leave TotalSessions
        // blank-equivalent by using a very high number staff won't hit — simplest is to let
        // DefaultTotalSessions be null when RequiresMultipleSessions is on but the treatment
        // has no fixed session count. Here we treat "1" typed by staff as "not fixed" → null.
        int? resolvedTotalSessions = RequiresMultipleSessions
            ? (TotalSessions > 1 ? TotalSessions : null)
            : null;

        int? resolvedInterval = RequiresMultipleSessions && FollowupIntervalDays > 0
            ? FollowupIntervalDays
            : null;

        if (!string.IsNullOrWhiteSpace(ServiceId))
        {
            var list = await _supabase.GetServicesAsync();
            var service = list.FirstOrDefault(s => s.Id == ServiceId);
            if (service != null)
            {
                service.Name = ServiceName;
                service.BasePrice = ServicePrice;
                service.Description = ServiceDescription;
                service.RequiresMultipleSessions = RequiresMultipleSessions;
                service.DefaultTotalSessions = resolvedTotalSessions;
                service.FollowupIntervalDays = resolvedInterval;

                var success = await _supabase.UpdateServiceAsync(service);
                if (!success)
                {
                    await Shell.Current.DisplayAlert("Error", "Could not update the service.", "OK");
                    return;
                }
            }
        }
        else
        {
            var newService = await _supabase.AddServiceAsync(new SupabaseService
            {
                Name = ServiceName,
                BasePrice = ServicePrice,
                Description = ServiceDescription,
                IsActive = true,
                RequiresMultipleSessions = RequiresMultipleSessions,
                DefaultTotalSessions = resolvedTotalSessions,
                FollowupIntervalDays = resolvedInterval,
                CreatedAt = DateTime.UtcNow
            });

            if (newService == null)
            {
                await Shell.Current.DisplayAlert("Error", "Could not save the service.", "OK");
                return;
            }
        }

        await Shell.Current.GoToAsync("..");
    }

    [RelayCommand]
    async Task Cancel() => await Shell.Current.GoToAsync("..");
}