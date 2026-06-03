using ClinicApp.Models;
using ClinicApp.Services;
using ClinicApp.Views.ServicesRelated;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using System.Diagnostics;

namespace ClinicApp.ViewModels.ServicesRelatedVM;

public partial class ServiceViewModel : ObservableObject
{
    readonly DatabaseService _db;

    [ObservableProperty] private bool isBusy;
    [ObservableProperty] private bool isRefreshing;

    public ObservableCollection<ServiceCardViewModel> ServiceCards { get; set; } = new();

    public ServiceViewModel(DatabaseService db) => _db = db;

    [RelayCommand]
    async Task LoadServices()
    {
        try
        {
            var serviceList = await _db.GetServices();

            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                ServiceCards.Clear();
                foreach (var s in serviceList)
                    ServiceCards.Add(new ServiceCardViewModel(s));
            });
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Database Error: {ex.Message}");
        }
        finally
        {
            isBusy = false;
            isRefreshing = false;
        }
    }

    [RelayCommand]
    void ToggleServiceCard(ServiceCardViewModel card)
    {
        if (card == null) return;
        bool wasExpanded = card.IsExpanded;
        foreach (var s in ServiceCards)
            s.IsExpanded = false;
        if (!wasExpanded)
            card.IsExpanded = true;
    }

    [RelayCommand]
    async Task EditService(ServiceCardViewModel card)
    {
        if (card == null) return;
        await Shell.Current.GoToAsync($"{nameof(AddServicePage)}?ServiceId={card.Service.ServiceID}");
    }

    [RelayCommand]
    async Task DeleteService(ServiceCardViewModel card)
    {
        if (card == null) return;

        bool answer = await Shell.Current.DisplayAlert("Confirm Delete",
            $"Are you sure you want to delete {card.Service.ServiceName}?", "Yes", "No");

        if (answer)
        {
            try { await _db.DeleteService(card.Service); }
            catch (Exception ex) { Debug.WriteLine($"[Delete] {ex.Message}"); }
            await LoadServices();
        }
    }

    [RelayCommand]
    async Task GoToAddService() =>
        await Shell.Current.GoToAsync(nameof(AddServicePage));
}
