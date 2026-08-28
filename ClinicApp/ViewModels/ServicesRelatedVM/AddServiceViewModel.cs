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

    // Injects the Supabase data service used for reading/writing services.
    public AddServiceViewModel(SupabaseDataService supabase) => _supabase = supabase;

    [ObservableProperty] string pageTitle = "Add Service";
    [ObservableProperty] string? serviceId;
    [ObservableProperty] string? serviceName;
    [ObservableProperty] decimal servicePrice;
    [ObservableProperty] string? serviceDescription;

    // Tracks whether the user has made any unsaved edits, so Cancel / the
    // back arrow know whether a "discard changes?" prompt is actually needed.
    bool _isLoading;
    bool _isDirty;

    // Flags the form as dirty when the name field changes.
    partial void OnServiceNameChanged(string? value) => MarkDirty();
    // Flags the form as dirty when the price field changes.
    partial void OnServicePriceChanged(decimal value) => MarkDirty();
    // Flags the form as dirty when the description field changes.
    partial void OnServiceDescriptionChanged(string? value) => MarkDirty();

    // Marks the form dirty, unless we're still loading initial data.
    void MarkDirty()
    {
        if (!_isLoading)
            _isDirty = true;
    }

    // Automatically called when ServiceId is set via navigation query param
    partial void OnServiceIdChanged(string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            PageTitle = "Edit Service";
            _ = LoadServiceDataAsync(value);
        }
    }

    // Loads an existing service's data into the form fields for editing.
    private async Task LoadServiceDataAsync(string id)
    {
        var list = await _supabase.GetServicesAsync();
        var service = list.FirstOrDefault(s => s.Id == id);
        if (service != null)
        {
            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                _isLoading = true;
                ServiceName = service.Name;
                ServicePrice = service.BasePrice;
                ServiceDescription = service.Description;
                _isLoading = false;
                _isDirty = false; // freshly loaded data isn't a user edit
            });
        }
    }

    // ─── Save command ────────────────────────────────────────
    // Validates the form, saves to Supabase, then confirms and navigates back.

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

        _isDirty = false;

        await Shell.Current.DisplayAlert(
            "Saved",
            PageTitle == "Edit Service"
                ? "The service has been updated successfully."
                : "The service has been saved successfully.",
            "OK");

        await Shell.Current.GoToAsync("..");
    }

    // ─── Cancel command ──────────────────────────────────────

    [RelayCommand]
    async Task Cancel()
    {
        if (_isDirty)
        {
            bool discard = await Shell.Current.DisplayAlert(
                "Discard changes?",
                "Are you sure you want to discard the changes you made?",
                "Discard", "Keep Editing");

            if (!discard)
                return;
        }

        await Shell.Current.GoToAsync("..");
    }
}
