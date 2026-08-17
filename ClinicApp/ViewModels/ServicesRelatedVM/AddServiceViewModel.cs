using ClinicApp.Models;
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

        if (!string.IsNullOrWhiteSpace(ServiceId))
        {
            var list = await _supabase.GetServicesAsync();
            var service = list.FirstOrDefault(s => s.Id == ServiceId);
            if (service != null)
            {
                service.Name = ServiceName;
                service.BasePrice = ServicePrice;
                service.Description = ServiceDescription;
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