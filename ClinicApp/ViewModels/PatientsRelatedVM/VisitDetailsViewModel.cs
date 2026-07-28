using ClinicApp.Helpers;
using ClinicApp.Models;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;

namespace ClinicApp.ViewModels.PatientsRelatedVM;

public partial class VisitDetailsViewModel : ObservableObject
{
    public ObservableCollection<TreatmentHistory> Treatments { get; }
        = new();

    [ObservableProperty]
    DateTime visitDate;

    [ObservableProperty]
    string visitTitle = "";

    public VisitDetailsViewModel()
    {
        LoadVisit();
    }

    private void LoadVisit()
    {
        var visit = VisitHistoryStore.Current;

        if (visit == null)
            return;

        VisitDate = visit.VisitDate;
        VisitTitle = visit.VisitTitle;

        Treatments.Clear();

        foreach (var item in visit.Treatments)
            Treatments.Add(item);
    }

    [RelayCommand]
    async Task Back()
    {
        await Shell.Current.GoToAsync("..");
    }
}