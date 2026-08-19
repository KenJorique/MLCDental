using ClinicApp.Models;
using ClinicApp.Services;
using ClinicApp.Views.Shared;
using ClinicApp.Views.ServicesRelated;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using System.Diagnostics;

namespace ClinicApp.ViewModels.ServicesRelatedVM;

public partial class ServiceViewModel : ObservableObject
{
    readonly SupabaseDataService _supabase;

    [ObservableProperty] private bool isBusy;
    [ObservableProperty] private bool isRefreshing;

    public ObservableCollection<ServiceCardViewModel> ServiceCards { get; set; } = new();

    public ServiceViewModel(SupabaseDataService supabase) => _supabase = supabase;

    [RelayCommand]
    public async Task LoadServices()
    {
        try
        {
            var serviceList = await _supabase.GetServicesAsync();
            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                ServiceCards.Clear();
                foreach (var s in serviceList)
                    ServiceCards.Add(new ServiceCardViewModel(s));
            });
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[Supabase] LoadServices: {ex.Message}");
        }
        finally
        {
            isBusy = false;
            isRefreshing = false;
        }
    }

    // Tap on card → open action sheet
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

    [RelayCommand]
    async Task GoToAddService() =>
        await Shell.Current.GoToAsync(nameof(AddServicePage));
}