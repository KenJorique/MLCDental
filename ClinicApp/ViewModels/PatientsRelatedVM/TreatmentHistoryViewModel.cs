using ClinicApp.Models;
using ClinicApp.Services;
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
        if (PatientId <= 0 || IsBusy) return; // Prevent concurrent loads

        IsBusy = true;
        try
        {
            var entries = await _db.GetTreatmentHistoryForPatient(PatientId);

            // Clear and add on the Main Thread to be safe
            MainThread.BeginInvokeOnMainThread(() => {
                History.Clear();
                foreach (var entry in entries)
                    History.Add(new TreatmentHistoryItemViewModel(entry));

                IsHistoryEmpty = History.Count == 0;
                HistoryCountText = History.Count == 1 ? "1 record" : $"{History.Count} records";
            });
        }
        finally { IsBusy = false; }
    }
}

/// <summary>
/// Per-row display wrapper for a TreatmentHistory record.
/// </summary>
public class TreatmentHistoryItemViewModel
{
    public TreatmentHistory Record { get; }

    public TreatmentHistoryItemViewModel(TreatmentHistory record)
    {
        Record = record;
    }

    public string ToothLabel => $"Tooth #{Record.ToothNumber}";
    public string ToothName => Record.ToothName;
    public string Condition => Record.Condition;
    public string Notes => Record.Notes;
    public string ActionType => Record.ActionType;
    public bool HasNotes => !string.IsNullOrWhiteSpace(Record.Notes);
    public bool HasPreviousCondition =>
        !string.IsNullOrWhiteSpace(Record.PreviousCondition) &&
        Record.PreviousCondition != Record.Condition &&
        Record.ActionType != "Added";

    public string PreviousConditionDisplay =>
        HasPreviousCondition ? $"was: {Record.PreviousCondition}" : string.Empty;

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
            try { return Color.FromArgb(Record.Color); }
            catch { return Colors.White; }
        }
    }

    public Color ActionBadgeColor => ActionType switch
    {
        "Added" => Color.FromArgb("#22C55E"),
        "Updated" => Color.FromArgb("#F59E0B"),
        "Completed" => Color.FromArgb("#EF4444"),
        _ => Color.FromArgb("#6B7A9A"),
    };
}
