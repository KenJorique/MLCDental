using ClinicApp.Helpers;
using ClinicApp.Models;
using ClinicApp.Services;
using ClinicApp.Views.PatientsRelated;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;

namespace ClinicApp.ViewModels.PatientsRelatedVM;

[QueryProperty(nameof(PatientId), "patientId")]
[QueryProperty(nameof(PatientName), "patientName")]
public partial class TreatmentHistoryViewModel : ObservableObject
{
    private readonly DatabaseService _db;

    [ObservableProperty] private int patientId;
    [ObservableProperty] private string patientName = string.Empty;
    [ObservableProperty] private bool isBusy;
    [ObservableProperty] private bool isHistoryEmpty = true;
    [ObservableProperty] private string historyCountText = "0 records";


    public ObservableCollection<TreatmentHistoryItemViewModel> History { get; } = new();
    public ObservableCollection<TreatmentVisitGroup> Visits { get; }
    = new();

    public TreatmentHistoryViewModel(DatabaseService db)
    {
        _db = db;
    }

    partial void OnPatientIdChanged(int value)
    {
        if (value > 0) LoadHistoryCommand.ExecuteAsync(null);
    }

    [RelayCommand]
    public async Task LoadHistoryAsync()
    {
        if (PatientId <= 0 || IsBusy) return;

        IsBusy = true;
        try
        {
            var entries = await _db.GetTreatmentHistoryForPatient(PatientId);

            MainThread.BeginInvokeOnMainThread(() =>
            {
                History.Clear();
                Visits.Clear();

                foreach (var entry in entries)
                    History.Add(new TreatmentHistoryItemViewModel(entry));

                foreach (var group in entries
                    .OrderByDescending(x => DateTime.Parse(x.Timestamp))
                    .GroupBy(x => DateTime.Parse(x.Timestamp).Date))
                {
                    var visit = new TreatmentVisitGroup
                    {
                        VisitDate = group.Key
                    };

                    foreach (var item in group)
                        visit.Treatments.Add(item);

                    Visits.Add(visit);
                }

                IsHistoryEmpty = History.Count == 0;
                HistoryCountText = History.Count == 1
                    ? "1 record"
                    : $"{History.Count} records";
            });
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    async Task OpenVisit(TreatmentVisitGroup visit)
    {
        if (visit == null)
            return;

        VisitHistoryStore.Current = visit;

        await Shell.Current.GoToAsync(nameof(VisitDetailsPage));
    }

}

/// <summary>
/// Per-row display wrapper for a TreatmentHistory record.
/// </summary>
public class TreatmentHistoryItemViewModel: ObservableObject
{
    public TreatmentHistory Record { get; }

    public TreatmentHistoryItemViewModel(TreatmentHistory record)
    {
        Record = record;
    }

    /// <summary>
    /// True if this record is a general service rather than a tooth treatment.
    /// </summary>
    public bool IsGeneralService => Record.ActionType == "Service";

    public string ToothLabel =>
        IsGeneralService
            ? "Service Rendered"
            : $"Tooth #{Record.ToothNumber}";

    public string ToothName =>
        IsGeneralService
            ? Record.Description
            : Record.ToothName;

    public string Condition =>
        IsGeneralService
            ? "General Service"
            : Record.Condition;

    public string Notes => Record.Notes;

    public string ActionType => Record.ActionType;

    public bool HasNotes =>
        !string.IsNullOrWhiteSpace(Record.Notes);

    public bool HasPreviousCondition =>
        !IsGeneralService &&
        !string.IsNullOrWhiteSpace(Record.PreviousCondition) &&
        Record.PreviousCondition != Record.Condition &&
        Record.ActionType != "Added";

    public string PreviousConditionDisplay =>
        HasPreviousCondition
            ? $"was: {Record.PreviousCondition}"
            : string.Empty;

    public string DateDisplay
    {
        get
        {
            if (DateTime.TryParse(Record.Timestamp, out var dt))
                return dt.ToString("MMM dd, yyyy");

            return Record.Timestamp;
        }
    }

    public string TimeDisplay
    {
        get
        {
            if (DateTime.TryParse(Record.Timestamp, out var dt))
                return dt.ToString("hh:mm tt");

            return string.Empty;
        }
    }

    public Color ConditionColor
    {
        get
        {
            try
            {
                return Color.FromArgb(Record.Color);
            }
            catch
            {
                return Colors.White;
            }
        }
    }

    public Color ActionBadgeColor => ActionType switch
    {
        "Added" => Color.FromArgb("#22C55E"),
        "Updated" => Color.FromArgb("#F59E0B"),
        "Completed" => Color.FromArgb("#EF4444"),
        "Service" => Color.FromArgb("#3B82F6"),
        _ => Color.FromArgb("#6B7280")
    };
}
