using ClinicApp.Models.PatientModels;
using ClinicApp.Models.SupabaseModels;
using ClinicApp.Services;
using ClinicApp.Views.Shared;
using CommunityToolkit.Maui.Views;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace ClinicApp.ViewModels.ServicesRelatedVM;

[QueryProperty(nameof(ServiceId), "ServiceId")]
public partial class AddServiceViewModel : ObservableObject
{
    readonly SupabaseDataService _supabase;

    // Injects the Supabase data service used for reading/writing services.
    public AddServiceViewModel(SupabaseDataService supabase) => _supabase = supabase;

    // "Add Service" or "Edit Service", set once ServiceId resolves.
    [ObservableProperty] string pageTitle = "Add Service";
    // Non-null only when editing an existing service.
    [ObservableProperty] string? serviceId;
    // Bound to the Service Name input.
    [ObservableProperty] string? serviceName;
    // Bound to the Price input.
    [ObservableProperty] decimal servicePrice;
    // Bound to the optional Description input.
    [ObservableProperty] string? serviceDescription;

    // ── Multi-session configuration ──
    // Toggles visibility of the session-count/interval fields.
    [ObservableProperty] bool requiresMultipleSessions;
    // How many sessions this service normally takes.
    [ObservableProperty] int totalSessions = 2;
    // Suggested gap in days between sessions.
    [ObservableProperty] int followupIntervalDays = 14;

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

    // Flags the form as dirty when multi-session is toggled, and refreshes ShowSessionFields.
    partial void OnRequiresMultipleSessionsChanged(bool value)
    {
        MarkDirty();
        OnPropertyChanged(nameof(ShowSessionFields));
    }
    // Flags the form as dirty when the total-sessions count changes; also refreshes the -/+ button enabled states.
    partial void OnTotalSessionsChanged(int value)
    {
        MarkDirty();
        OnPropertyChanged(nameof(CanDecrementTotalSessions));
        OnPropertyChanged(nameof(CanIncrementTotalSessions));
        DecrementTotalSessionsCommand.NotifyCanExecuteChanged();
        IncrementTotalSessionsCommand.NotifyCanExecuteChanged();
    }
    // Flags the form as dirty when the follow-up interval changes; also refreshes the -/+ button enabled states.
    partial void OnFollowupIntervalDaysChanged(int value)
    {
        MarkDirty();
        OnPropertyChanged(nameof(CanDecrementFollowupInterval));
        OnPropertyChanged(nameof(CanIncrementFollowupInterval));
        DecrementFollowupIntervalCommand.NotifyCanExecuteChanged();
        IncrementFollowupIntervalCommand.NotifyCanExecuteChanged();
    }

    // Marks the form dirty, unless we're still loading initial data.
    void MarkDirty()
    {
        if (!_isLoading)
            _isDirty = true;
    }

    // Controls visibility of the session-count/interval fields in the UI.
    public bool ShowSessionFields => RequiresMultipleSessions;

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
                RequiresMultipleSessions = service.RequiresMultipleSessions;
                TotalSessions = service.DefaultTotalSessions ?? 2;
                FollowupIntervalDays = service.FollowupIntervalDays ?? 14;
                OnPropertyChanged(nameof(ShowSessionFields));
                _isLoading = false;
                _isDirty = false; // freshly loaded data isn't a user edit
            });
        }
    }

    // Shows a plain OK-only popup (validation errors, save errors, success messages) — always the green "OK" style since it's not a destructive action.
    private static async Task ShowAlertAsync(string title, string message)
    {
        var popup = new ConfirmationPopup(title, message, confirmText: "OK", showCancelButton: false);
        await Shell.Current.ShowPopupAsync(popup);
    }

    // Tapped from the small info icons next to a field label — shows the full explanation in a popup instead of a permanent gray caption.
    [RelayCommand]
    async Task ShowHelp(string? message)
    {
        if (string.IsNullOrWhiteSpace(message)) return;
        await ShowAlertAsync("Info", message);
    }

    // Whether Total Sessions is above its floor of 2 — drives both the − button's enabled state and its command.
    public bool CanDecrementTotalSessions => TotalSessions > 2;
    // Whether Total Sessions is below its ceiling of 12 — drives both the + button's enabled state and its command.
    public bool CanIncrementTotalSessions => TotalSessions < 12;
    // Whether the follow-up interval is above its floor of 1 day — drives both the − button's enabled state and its command.
    public bool CanDecrementFollowupInterval => FollowupIntervalDays > 1;
    // Whether the follow-up interval is below its ceiling of 180 days — drives both the + button's enabled state and its command.
    public bool CanIncrementFollowupInterval => FollowupIntervalDays < 180;

    // − button for Total Sessions, clamped to the same Minimum the old Stepper used.
    [RelayCommand(CanExecute = nameof(CanDecrementTotalSessions))]
    void DecrementTotalSessions() => TotalSessions = Math.Max(TotalSessions - 1, 2);

    // + button for Total Sessions, clamped to the same Maximum the old Stepper used.
    [RelayCommand(CanExecute = nameof(CanIncrementTotalSessions))]
    void IncrementTotalSessions() => TotalSessions = Math.Min(TotalSessions + 1, 12);

    // − button for the follow-up interval, clamped to the same Minimum the old Stepper used.
    [RelayCommand(CanExecute = nameof(CanDecrementFollowupInterval))]
    void DecrementFollowupInterval() => FollowupIntervalDays = Math.Max(FollowupIntervalDays - 1, 1);

    // + button for the follow-up interval, clamped to the same Maximum the old Stepper used.
    [RelayCommand(CanExecute = nameof(CanIncrementFollowupInterval))]
    void IncrementFollowupInterval() => FollowupIntervalDays = Math.Min(FollowupIntervalDays + 1, 180);

    // Validates, saves (or updates) the service, and logs the activity.
    [RelayCommand]
    async Task Save()
    {
        if (string.IsNullOrWhiteSpace(ServiceName))
        {
            await ShowAlertAsync("Validation", "Service name is required.");
            return;
        }
        if (ServicePrice <= 0)
        {
            await ShowAlertAsync("Validation", "Please enter a valid price.");
            return;
        }
        // Session count only matters for services that actually require multiple sessions.
        if (RequiresMultipleSessions && TotalSessions < 2)
        {
            await ShowAlertAsync("Validation", "Multi-session services need at least 2 sessions.");
            return;
        }

        // Confirm before committing — green Confirm button is the popup's default, matching "green for save".
        var confirmPopup = new ConfirmationPopup(
            "Save Service?",
            PageTitle == "Edit Service"
                ? $"Save changes to \"{ServiceName}\"?"
                : $"Add \"{ServiceName}\" as a new service?",
            confirmText: "Save");

        var confirmResult = await Shell.Current.ShowPopupAsync(confirmPopup);
        if (confirmResult is not bool confirmed || !confirmed) return;

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
                var oldPrice = service.BasePrice;

                service.Name = ServiceName;
                service.BasePrice = ServicePrice;
                service.Description = ServiceDescription;
                service.RequiresMultipleSessions = RequiresMultipleSessions;
                service.DefaultTotalSessions = resolvedTotalSessions;
                service.FollowupIntervalDays = resolvedInterval;

                var success = await _supabase.UpdateServiceAsync(service);
                if (!success)
                {
                    await ShowAlertAsync("Error", "Could not update the service.");
                    return;
                }

                if (oldPrice != ServicePrice)
                    await _supabase.LogActivityAsync("ServicePriceChanged",
                        $"{ServiceName}'s price changed from ₱{oldPrice:N2} to ₱{ServicePrice:N2}");
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
                await ShowAlertAsync("Error", "Could not save the service.");
                return;
            }

            await _supabase.LogActivityAsync("NewService", $"New service {ServiceName} added");
        }

        _isDirty = false;

        await ShowAlertAsync(
            "Saved",
            PageTitle == "Edit Service"
                ? "The service has been updated successfully."
                : "The service has been saved successfully.");

        await Shell.Current.GoToAsync("..");
    }

    // Confirms discard if there are unsaved edits, then goes back.
    [RelayCommand]
    async Task Cancel()
    {
        if (_isDirty)
        {
            // Red Confirm button — this is a destructive/discard action.
            var popup = new ConfirmationPopup(
            "Discard Changes?",
            "Are you sure you want to discard the changes you made?",
            confirmText: "Discard",
            confirmColor: Color.FromArgb("#DC143C"));

            var result = await Shell.Current.ShowPopupAsync(popup);
            if (result is not bool discard || !discard)
                return;
        }

        await Shell.Current.GoToAsync("..");
    }
}
