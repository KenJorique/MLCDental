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

    public ServiceSummaryViewModel()
    {
        LoadDraft();
    }

    public void LoadDraft()
    {
        Services.Clear();

        var draft = BillDraftStore.Current;
        if (draft == null)
            return;

        PatientName = draft.PatientName;

        foreach (var item in draft.Services)
            Services.Add(item);

        OnPropertyChanged(nameof(Subtotal));
    }

    [RelayCommand]
    void RemoveService(ServiceLineItem item)
    {
        if (item == null)
            return;

        Services.Remove(item);

        BillDraftStore.Current?.Services.Remove(item);

        OnPropertyChanged(nameof(Subtotal));
    }

    [RelayCommand]
    async Task Continue()
    {
        if (BillDraftStore.Current == null)
            return;

        BillDraftStore.Current.Services.Clear();

        foreach (var item in Services)
            BillDraftStore.Current.Services.Add(item);

        await Shell.Current.GoToAsync(nameof(BillSummaryPage));
    }

    [RelayCommand]
    async Task Back()
    {
        await Shell.Current.GoToAsync("..");
    }
}