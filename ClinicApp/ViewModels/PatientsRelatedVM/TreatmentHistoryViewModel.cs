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

    // =========================================================
    // PATIENT
    // =========================================================

    [ObservableProperty]
    private int patientId;

    [ObservableProperty]
    private string patientName = string.Empty;

    public string PatientInitials
    {
        get
        {
            if (string.IsNullOrWhiteSpace(PatientName))
                return "?";

            var parts = PatientName
                .Split(' ', StringSplitOptions.RemoveEmptyEntries);

            if (parts.Length == 1)
                return parts[0][0].ToString().ToUpperInvariant();

            return $"{parts[0][0]}{parts[^1][0]}"
                .ToUpperInvariant();
        }
    }


    // =========================================================
    // STATE
    // =========================================================

    [ObservableProperty]
    private bool isBusy;

    [ObservableProperty]
    private bool isHistoryEmpty = true;

    [ObservableProperty]
    private bool hasHistory;

    [ObservableProperty]
    private string historyCountText = "0 records";


    // =========================================================
    // COLLECTIONS
    // =========================================================

    public ObservableCollection<TreatmentHistoryItemViewModel> History { get; }
        = new();

    public ObservableCollection<TreatmentVisitGroup> Visits { get; }
        = new();


    // =========================================================
    // CONSTRUCTOR
    // =========================================================

    public TreatmentHistoryViewModel(DatabaseService db)
    {
        _db = db;
    }


    // =========================================================
    // PROPERTY CHANGED
    // =========================================================

    partial void OnPatientIdChanged(int value)
    {
        if (value > 0)
        {
            _ = LoadHistoryAsync();
        }
    }

    partial void OnPatientNameChanged(string value)
    {
        OnPropertyChanged(nameof(PatientInitials));
    }


    // =========================================================
    // LOAD HISTORY
    // =========================================================

    [RelayCommand]
    public async Task LoadHistoryAsync()
    {
        if (PatientId <= 0 || IsBusy)
            return;

        IsBusy = true;

        try
        {
            var entries =
                await _db.GetTreatmentHistoryForPatient(PatientId);

            entries ??= new List<TreatmentHistory>();

            // -------------------------------------------------
            // Parse timestamps safely
            // -------------------------------------------------

            var parsedEntries = entries
                .Select(entry =>
                {
                    DateTime parsedDate;

                    if (!DateTime.TryParse(
                            entry.Timestamp,
                            out parsedDate))
                    {
                        parsedDate = DateTime.MinValue;
                    }

                    return new
                    {
                        Entry = entry,
                        Date = parsedDate
                    };
                })
                .OrderByDescending(x => x.Date)
                .ToList();


            // -------------------------------------------------
            // Update UI
            // -------------------------------------------------

            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                History.Clear();
                Visits.Clear();

                // =============================================
                // FLAT HISTORY
                // =============================================

                foreach (var item in parsedEntries)
                {
                    History.Add(
                        new TreatmentHistoryItemViewModel(
                            item.Entry));
                }


                // =============================================
                // GROUP BY VISIT DATE
                // =============================================

                var validEntries = parsedEntries
                    .Where(x => x.Date != DateTime.MinValue)
                    .GroupBy(x => x.Date.Date)
                    .OrderByDescending(x => x.Key);

                foreach (var group in validEntries)
                {
                    var visit = new TreatmentVisitGroup
                    {
                        VisitDate = group.Key
                    };

                    foreach (var item in group)
                    {
                        visit.Treatments.Add(item.Entry);
                    }

                    Visits.Add(visit);
                }


                // =============================================
                // STATE
                // =============================================

                var count = History.Count;

                IsHistoryEmpty = count == 0;
                HasHistory = count > 0;

                HistoryCountText = count switch
                {
                    0 => "0 records",
                    1 => "1 record",
                    _ => $"{count} records"
                };
            });
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine(
                $"[TreatmentHistory] Load error: {ex}");

            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                History.Clear();
                Visits.Clear();

                IsHistoryEmpty = true;
                HasHistory = false;
                HistoryCountText = "0 records";
            });
        }
        finally
        {
            IsBusy = false;
        }
    }


    // =========================================================
    // OPEN VISIT
    // =========================================================

    [RelayCommand]
    private async Task OpenVisit(TreatmentVisitGroup? visit)
    {
        if (visit == null)
            return;

        try
        {
            VisitHistoryStore.Current = visit;

            await Shell.Current.GoToAsync(
                nameof(VisitDetailsPage));
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine(
                $"[TreatmentHistory] OpenVisit error: {ex}");
        }
    }
}


// =============================================================
// TREATMENT HISTORY ITEM VIEW MODEL
// =============================================================

public class TreatmentHistoryItemViewModel : ObservableObject
{
    public TreatmentHistory Record { get; }


    public TreatmentHistoryItemViewModel(
        TreatmentHistory record)
    {
        Record = record;
    }


    // =========================================================
    // TYPE
    // =========================================================

    /// <summary>
    /// Determines whether this history record represents
    /// a general service rather than a tooth-specific treatment.
    /// </summary>
    public bool IsGeneralService =>
        string.Equals(
            Record.ActionType,
            "Service",
            StringComparison.OrdinalIgnoreCase);


    // =========================================================
    // MAIN DISPLAY
    // =========================================================

    public string ToothLabel =>
        IsGeneralService
            ? "SERVICE RENDERED"
            : $"TOOTH #{Record.ToothNumber}";


    public string ToothName
    {
        get
        {
            if (IsGeneralService)
            {
                if (!string.IsNullOrWhiteSpace(
                        Record.Description))
                {
                    return Record.Description;
                }

                return "Service";
            }

            return string.IsNullOrWhiteSpace(
                       Record.ToothName)
                ? "Unknown Tooth"
                : Record.ToothName;
        }
    }


    public string Condition =>
        IsGeneralService
            ? "General Service"
            : string.IsNullOrWhiteSpace(
                  Record.Condition)
                ? "No condition recorded"
                : Record.Condition;


    public string Notes =>
        Record.Notes ?? string.Empty;


    public string ActionType =>
        string.IsNullOrWhiteSpace(
            Record.ActionType)
            ? "Record"
            : Record.ActionType;


    // =========================================================
    // NOTES
    // =========================================================

    public bool HasNotes =>
        !string.IsNullOrWhiteSpace(
            Record.Notes);


    // =========================================================
    // PREVIOUS CONDITION
    // =========================================================

    public bool HasPreviousCondition =>
        !IsGeneralService &&
        !string.IsNullOrWhiteSpace(
            Record.PreviousCondition) &&
        !string.Equals(
            Record.PreviousCondition,
            Record.Condition,
            StringComparison.OrdinalIgnoreCase) &&
        !string.Equals(
            Record.ActionType,
            "Added",
            StringComparison.OrdinalIgnoreCase);


    public string PreviousConditionDisplay =>
        HasPreviousCondition
            ? $"Previous: {Record.PreviousCondition}"
            : string.Empty;


    // =========================================================
    // DATE
    // =========================================================

    public string DateDisplay
    {
        get
        {
            if (DateTime.TryParse(
                    Record.Timestamp,
                    out var date))
            {
                return date.ToString(
                    "MMM dd, yyyy");
            }

            return string.IsNullOrWhiteSpace(
                       Record.Timestamp)
                ? string.Empty
                : Record.Timestamp;
        }
    }


    // =========================================================
    // TIME
    // =========================================================

    public string TimeDisplay
    {
        get
        {
            if (DateTime.TryParse(
                    Record.Timestamp,
                    out var date))
            {
                return date.ToString(
                    "hh:mm tt");
            }

            return string.Empty;
        }
    }


    // =========================================================
    // CONDITION COLOR
    // =========================================================

    public Color ConditionColor
    {
        get
        {
            if (string.IsNullOrWhiteSpace(
                    Record.Color))
            {
                return Color.FromArgb(
                    "#CBD5E1");
            }

            try
            {
                return Color.FromArgb(
                    Record.Color);
            }
            catch
            {
                return Color.FromArgb(
                    "#CBD5E1");
            }
        }
    }


    // =========================================================
    // ACTION BADGE COLOR
    // =========================================================

    public Color ActionBadgeColor
    {
        get
        {
            return ActionType.ToLowerInvariant() switch
            {
                "added" =>
                    Color.FromArgb("#22C55E"),

                "updated" =>
                    Color.FromArgb("#F59E0B"),

                "completed" =>
                    Color.FromArgb("#EF4444"),

                "service" =>
                    Color.FromArgb("#3B82F6"),

                "deleted" =>
                    Color.FromArgb("#EF4444"),

                "cleared" =>
                    Color.FromArgb("#64748B"),

                "cancelled" =>
                    Color.FromArgb("#64748B"),

                _ =>
                    Color.FromArgb("#6B7280")
            };
        }
    }
}
