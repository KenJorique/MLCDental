using ClinicApp.Helpers;
using ClinicApp.Models;
using ClinicApp.Views.TransactionRelated;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;

namespace ClinicApp.ViewModels.TransactionVM;

public partial class ServiceSummaryViewModel : ObservableObject
{
    [ObservableProperty]
    private string patientName = string.Empty;

    [ObservableProperty]
    private bool isBusy;

    public ObservableCollection<ServiceLineItem> Services { get; } = new();

    public decimal Subtotal => Services.Sum(x => x.Subtotal);

    public int TotalItems => Services.Count;

    public bool HasServices => Services.Count > 0;

    public ServiceSummaryViewModel()
    {
        Services.CollectionChanged += (_, _) => RefreshDerivedState();
        LoadDraft();
    }

    public void LoadDraft()
    {
        Services.Clear();

        var draft = BillDraftStore.Current;
        if (draft == null)
        {
            RefreshDerivedState();
            return;
        }

        PatientName = draft.PatientName;

        foreach (var item in draft.Services)
            Services.Add(item);

        RefreshDerivedState();
    }

    void RefreshDerivedState()
    {
        OnPropertyChanged(nameof(Subtotal));
        OnPropertyChanged(nameof(TotalItems));
        OnPropertyChanged(nameof(HasServices));
        ContinueCommand.NotifyCanExecuteChanged();
    }

    [RelayCommand]
    async Task RemoveService(ServiceLineItem item)
    {
        if (item == null)
            return;

        bool confirm = await Shell.Current.CurrentPage.DisplayAlert(
            "Remove Service",
            $"Remove \"{item.ServiceName}\" from this bill?",
            "Remove",
            "Cancel");

        if (!confirm)
            return;

        Services.Remove(item);
        BillDraftStore.Current?.Services.Remove(item);
        // RefreshDerivedState() fires automatically via CollectionChanged
    }

    bool CanContinue() => HasServices;

    [RelayCommand(CanExecute = nameof(CanContinue))]
    async Task Continue()
    {
        if (BillDraftStore.Current == null)
        {
            await Shell.Current.DisplayAlert("Debug", "BillDraftStore.Current is null", "OK");
            return;
        }

        try
        {
            BillDraftStore.Current.Services.Clear();

            foreach (var item in Services)
                BillDraftStore.Current.Services.Add(item);

            await Shell.Current.GoToAsync(nameof(BillSummaryPage));
        }
        catch (Exception ex)
        {
            await Shell.Current.DisplayAlert("Navigation failed", ex.Message, "OK");
        }
    }

    [RelayCommand]
    async Task Back()
    {
        await Shell.Current.GoToAsync("..");
    }
}