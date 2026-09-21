using ClinicApp.Models.PatientModels;
using ClinicApp.Models.TreatmentModels;
using ClinicApp.Services;
using ClinicApp.Views.Shared;
using CommunityToolkit.Maui.Views;
using ClinicApp.Services.Database;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;

namespace ClinicApp.ViewModels.DentalChart;

[QueryProperty(nameof(PatientId), "patientId")]
[QueryProperty(nameof(PatientName), "patientName")]
public partial class DentalChartViewModel : ObservableObject
{
    // ═══════════════════════════════════════════════════════════════
    // CONDITION COLORS
    // ═══════════════════════════════════════════════════════════════

    public static readonly Dictionary<string, string> ConditionColors =
        new(StringComparer.OrdinalIgnoreCase)
        {
            { "Normal",    "#FFFFFF" },
            { "Filling",   "#0000FF" }, // Blue
            { "Caries",    "#FF0000" }, // Red
            { "Completed", "#00FF00" }, // Green
            { "Missing",   "#000000" }, // Black

            // Additional treatment categories
            { "Root Canal", "#FF7A00" }, // Orange
            { "Crown",      "#A855F7" }, // Purple
            { "Bridge",     "#A855F7" }, // Purple
            { "Prosthetic", "#A855F7" }  // Purple
        };

    /// <summary>
    /// Conditions displayed in the edit Picker.
    /// </summary>
    public List<string> ConditionOptions { get; } = new()
    {
        "Normal",
        "Filling",
        "Caries",
        "Completed",
        "Missing",
        "Crown"
    };

    // One row in the Legend card — Color always resolved live from ConditionColors above,
    // so the swatch shown can never fall out of sync with the color actually applied to a tooth.
    public class LegendEntry
    {
        public string Label { get; init; } = string.Empty;
        public Color Color { get; init; } = Colors.White;
    }

    // Looks up a ConditionColors hex value and parses it, falling back to white if missing.
    private static Color ColorOf(string conditionKey) =>
        ConditionColors.TryGetValue(conditionKey, out var hex) ? Color.FromArgb(hex) : Colors.White;

    public List<LegendEntry> LegendItems { get; } = new()
    {
        new() { Label = "Normal / Untreated",         Color = ColorOf("Normal") },
        new() { Label = "Filling / Restoration",      Color = ColorOf("Filling") },
        new() { Label = "Active Decay / Caries",      Color = ColorOf("Caries") },
        new() { Label = "Completed Treatment",        Color = ColorOf("Completed") },
        new() { Label = "Missing / Extracted",        Color = ColorOf("Missing") },
        new() { Label = "Crown / Bridge / Prosthetic", Color = ColorOf("Crown") },
    };

    private readonly DatabaseService _db;
    private readonly SupabaseRealtimeService _realtimeService;


    // ═══════════════════════════════════════════════════════════════
    // PAGE STATE
    // ═══════════════════════════════════════════════════════════════

    [ObservableProperty]
    private int patientId;

    [ObservableProperty]
    private string patientName = string.Empty;

    [ObservableProperty]
    private bool isBusy;

    [ObservableProperty]
    private string conditionCountText = "0 conditions";

    // ═══════════════════════════════════════════════════════════════
    // TEETH
    // ═══════════════════════════════════════════════════════════════

    public ObservableCollection<ToothViewModel> UpperTeeth { get; }
        = new();

    public ObservableCollection<ToothViewModel> LowerTeeth { get; }
        = new();

    private readonly List<ToothViewModel> _allTeeth = new();

    // ═══════════════════════════════════════════════════════════════
    // MODAL STATE
    // ═══════════════════════════════════════════════════════════════

    [ObservableProperty]
    private bool isModalVisible;

    [ObservableProperty]
    private bool isEditMode;

    [ObservableProperty]
    private string modalToothTitle = string.Empty;

    [ObservableProperty]
    private string modalToothName = string.Empty;

    [ObservableProperty]
    private string modalCondition = string.Empty;

    // Nothing to clear on a tooth that's already Normal — the Clear button binds to this.
    public bool CanClearTooth =>
        !string.Equals(ModalCondition, "Normal", StringComparison.OrdinalIgnoreCase);

    // Auto-generated ModalCondition setter calls this — keep CanClearTooth in sync whenever
    // the modal's displayed condition changes (tooth tapped, edit saved, edit cancelled).
    partial void OnModalConditionChanged(string value) => OnPropertyChanged(nameof(CanClearTooth));

    [ObservableProperty]
    private Color modalConditionColor = Colors.White;

    [ObservableProperty]
    private string modalLastUpdated = string.Empty;

    [ObservableProperty]
    private string modalNotes = string.Empty;

    [ObservableProperty]
    private string editCondition = string.Empty;

    [ObservableProperty]
    private string editNotes = string.Empty;

    private ToothViewModel? _modalTooth;

    // ═══════════════════════════════════════════════════════════════
    // CONSTRUCTOR
    // ═══════════════════════════════════════════════════════════════
    public DentalChartViewModel(DatabaseService db, SupabaseRealtimeService realtimeService)
    {
        _db = db;
        _realtimeService = realtimeService;
        BuildTeeth();

        _realtimeService.OnToothRecordChanged += OnToothRecordChangedRemotely;
    }

    // Plain OK-only popup for error notices — matches the rest of the app's convention of
    // surfacing errors via popup instead of an inline status label.
    private static Task ShowNoticeAsync(string title, string message)
    {
        var popup = new ConfirmationPopup(title, message, "OK",
            Color.FromArgb("#2E7D32"), showCancelButton: false);
        return Shell.Current.CurrentPage.ShowPopupAsync(popup);
    }

    // Recomputes the "N conditions" pill from the teeth currently in memory. Called after
    // LoadChartAsync AND after Save/Clear — those change a tooth's Condition directly without
    // reloading the whole chart, so without this call the pill goes stale until the next visit
    // to this page (this was the "cleared a tooth but the count didn't go down" bug).
    private void RefreshConditionCount()
    {
        var affectedTeeth =
            _allTeeth.Count(t =>
                !string.Equals(t.Condition, "Normal", StringComparison.OrdinalIgnoreCase));

        ConditionCountText = $"{affectedTeeth} condition{(affectedTeeth == 1 ? "" : "s")}";
    }

    private async void OnToothRecordChangedRemotely()
    {
        if (PatientId > 0 && !IsBusy)
            await LoadChartAsync();
    }
    public void Cleanup()
    {
        _realtimeService.OnToothRecordChanged -= OnToothRecordChangedRemotely;
    }

    // ═══════════════════════════════════════════════════════════════
    // BUILD 32 TEETH
    // ═══════════════════════════════════════════════════════════════

    private void BuildTeeth()
    {
        for (int i = 1; i <= 32; i++)
        {
            var tooth = new ToothViewModel
            {
                ToothNumber = i
            };

            _allTeeth.Add(tooth);

            if (i <= 16)
                UpperTeeth.Add(tooth);
            else
                LowerTeeth.Add(tooth);
        }
    }

    partial void OnPatientIdChanged(int value)
    {
        if (value > 0)
            _ = LoadChartAsync();
    }

    // ═══════════════════════════════════════════════════════════════
    // LOAD DENTAL CHART
    // ═══════════════════════════════════════════════════════════════

    [RelayCommand]
    public async Task LoadChartAsync()
    {
        if (PatientId <= 0 || IsBusy)
            return;

        IsBusy = true;

        try
        {
            // -------------------------------------------------------
            // Reset all teeth first
            // -------------------------------------------------------

            foreach (var tooth in _allTeeth)
                tooth.Reset();

            // -------------------------------------------------------
            // Load CURRENT tooth records
            // -------------------------------------------------------

            var toothRecords =
                await _db.GetToothRecordsForPatient(PatientId);

            // -------------------------------------------------------
            // Load TREATMENT HISTORY
            //
            // This is important:
            //
            // TreatmentHistory contains things like:
            //
            // Tooth #12
            // Filled (Composite)
            //
            // We use this to color the dental chart.
            // -------------------------------------------------------

            var treatmentHistory =
                await _db.GetTreatmentHistoryForPatient(PatientId);

            // -------------------------------------------------------
            // First apply current ToothRecords
            // -------------------------------------------------------

            foreach (var record in toothRecords)
            {
                var tooth = _allTeeth.FirstOrDefault(
                    t => t.ToothNumber == record.ToothNumber);

                if (tooth == null)
                    continue;

                tooth.ApplyRecord(record);
            }

            // -------------------------------------------------------
            // Find the latest history record for each tooth.
            //
            // General services with ToothNumber = 0 are ignored.
            // -------------------------------------------------------

            var latestHistoryByTooth =
                treatmentHistory
                    .Where(x => x.ToothNumber > 0)
                    .GroupBy(x => x.ToothNumber)
                    .ToDictionary(
                        g => g.Key,
                        g => g
                            .OrderByDescending(GetTimestamp)
                            .FirstOrDefault());

            // -------------------------------------------------------
            // Apply TreatmentHistory to the chart
            // -------------------------------------------------------

            foreach (var pair in latestHistoryByTooth)
            {
                var toothNumber = pair.Key;
                var history = pair.Value;

                if (history == null)
                    continue;

                var tooth = _allTeeth.FirstOrDefault(
                    t => t.ToothNumber == toothNumber);

                if (tooth == null)
                    continue;

                // ---------------------------------------------------
                // If the latest history says CLEARED,
                // the tooth should appear normal.
                // ---------------------------------------------------

                if (history.ActionType.Equals(
                        "Cleared",
                        StringComparison.OrdinalIgnoreCase))
                {
                    tooth.Reset();
                    continue;
                }

                // ---------------------------------------------------
                // Convert the treatment history into a dental
                // chart condition.
                // ---------------------------------------------------

                var chartCondition =
                    GetChartCondition(history);

                // ---------------------------------------------------
                // Only override the current ToothRecord when the
                // treatment history is relevant.
                // ---------------------------------------------------

                if (chartCondition == "Normal")
                    continue;

                var historyColor =
                    GetConditionColor(chartCondition);

                var historyRecord = new ToothRecord
                {
                    PatientId = PatientId,
                    ToothNumber = toothNumber,
                    Condition = chartCondition,
                    Color = historyColor,
                    Notes = history.Notes ?? string.Empty,
                    LastUpdated = GetTimestamp(history)
                        .ToString("yyyy-MM-dd"),
                    DateUpdated = GetTimestamp(history)
                        .ToString("yyyy-MM-dd")
                };

                tooth.ApplyRecord(historyRecord);
            }

            RefreshConditionCount();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine(
                $"[DentalChart] Load error: {ex}");

            await ShowNoticeAsync("Unable to Load Chart",
                "Something went wrong loading this patient's dental chart. Please try again.");
        }
        finally
        {
            IsBusy = false;
        }
    }

    // ═══════════════════════════════════════════════════════════════
    // DETERMINE CHART CONDITION FROM TREATMENT HISTORY
    // ═══════════════════════════════════════════════════════════════

    // Convenience overload for a full TreatmentHistory record — combines its Condition and
    // Description before handing off to the shared text-based matcher below.
    private static string GetChartCondition(TreatmentHistory history) =>
        GetChartCondition(history.Condition, history.Description);

    // Shared bucket-matcher: turns any free-text condition/description into one of the fixed
    // ConditionColors keys. Public so callers outside this ViewModel (e.g. BillingService, when
    // it logs a treatment from a service name) resolve the SAME chart color this page would —
    // instead of doing their own exact-string lookup against ConditionColors that silently
    // misses whenever the wording doesn't match verbatim.
    public static string GetChartCondition(string condition, string description = "")
    {
        // -----------------------------------------------------------
        // Combine both fields into one text to scan.
        //
        // Example:
        // "Filled (Composite)"
        // "Caries"
        // "Root Canal Treatment"
        // -----------------------------------------------------------

        var text =
            $"{condition} {description}";

        text = text.Trim();

        if (string.IsNullOrWhiteSpace(text))
            return "Normal";

        // -----------------------------------------------------------
        // MISSING / EXTRACTION
        // -----------------------------------------------------------

        if (ContainsAny(text,
                "missing",
                "extracted",
                "extraction",
                "extract"))
        {
            return "Missing";
        }

        // -----------------------------------------------------------
        // ROOT CANAL
        // -----------------------------------------------------------

        if (ContainsAny(text,
                "root canal",
                "endodontic",
                "endodontics",
                "pulpotomy",
                "pulpectomy"))
        {
            return "Root Canal";
        }

        // -----------------------------------------------------------
        // CROWN / BRIDGE / PROSTHETIC
        // -----------------------------------------------------------

        if (ContainsAny(text,
                "crown",
                "bridge",
                "prosthetic",
                "prosthodontic",
                "veneer"))
        {
            return "Crown";
        }

        // -----------------------------------------------------------
        // CARIES / DECAY
        // -----------------------------------------------------------

        if (ContainsAny(text,
                "caries",
                "cavity",
                "decay",
                "decayed"))
        {
            return "Caries";
        }

        // -----------------------------------------------------------
        // FILLING / RESTORATION
        //
        // This catches:
        //
        // Filled (Composite)
        // Composite Filling
        // Filling
        // Restoration
        // Amalgam Filling
        // Resin Filling
        // -----------------------------------------------------------

        if (ContainsAny(text,
                "filling",
                "filled",
                "restoration",
                "composite",
                "amalgam",
                "resin",
                "sealant"))
        {
            return "Filling";
        }

        // -----------------------------------------------------------
        // COMPLETED
        // -----------------------------------------------------------

        if (ContainsAny(text,
                "completed",
                "complete",
                "done"))
        {
            return "Completed";
        }

        // -----------------------------------------------------------
        // If this is a generic service but it has no recognizable
        // dental condition, don't randomly color the tooth.
        // -----------------------------------------------------------

        return "Normal";
    }

    // ═══════════════════════════════════════════════════════════════
    // STRING MATCH HELPER
    // ═══════════════════════════════════════════════════════════════

    private static bool ContainsAny(
        string source,
        params string[] values)
    {
        foreach (var value in values)
        {
            if (source.Contains(
                    value,
                    StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    // ═══════════════════════════════════════════════════════════════
    // COLOR HELPER
    // ═══════════════════════════════════════════════════════════════

    // Public so BillingService can turn a chart bucket (from GetChartCondition above) into the
    // same hex color this page uses, instead of maintaining its own copy of this lookup.
    public static string GetConditionColor(string condition)
    {
        if (ConditionColors.TryGetValue(
                condition,
                out var color))
        {
            return color;
        }

        return "#FFFFFF";
    }

    // ═══════════════════════════════════════════════════════════════
    // SAFE TIMESTAMP PARSING
    // ═══════════════════════════════════════════════════════════════

    private static DateTime GetTimestamp(
        TreatmentHistory history)
    {
        if (DateTime.TryParse(
                history.Timestamp,
                out var timestamp))
        {
            return timestamp;
        }

        return DateTime.MinValue;
    }

    // ═══════════════════════════════════════════════════════════════
    // TOOTH TAP
    // ═══════════════════════════════════════════════════════════════

    [RelayCommand]
    private void ToothTapped(int toothNumber)
    {
        if (IsBusy)
            return;

        var tooth = _allTeeth.FirstOrDefault(
            t => t.ToothNumber == toothNumber);

        if (tooth == null)
            return;

        foreach (var t in _allTeeth)
            t.IsSelected = false;

        tooth.IsSelected = true;

        _modalTooth = tooth;

        ModalToothTitle =
            $"Tooth #{toothNumber}";

        ModalToothName =
            tooth.ToothName;

        ModalCondition =
            tooth.Condition;

        ModalConditionColor =
            GetColorFromCondition(tooth.Condition);

        ModalLastUpdated =
            string.IsNullOrWhiteSpace(tooth.LastUpdated)
                ? "Not recorded"
                : tooth.LastUpdated;

        ModalNotes =
            tooth.Notes;

        EditCondition =
            tooth.Condition;

        EditNotes =
            tooth.Notes;

        IsEditMode = false;
        IsModalVisible = true;
    }

    // ═══════════════════════════════════════════════════════════════
    // GET COLOR
    // ═══════════════════════════════════════════════════════════════

    private static Color GetColorFromCondition(
        string condition)
    {
        var hex = GetConditionColor(condition);

        try
        {
            return Color.FromArgb(hex);
        }
        catch
        {
            return Colors.White;
        }
    }

    // ═══════════════════════════════════════════════════════════════
    // CLOSE MODAL
    // ═══════════════════════════════════════════════════════════════

    [RelayCommand]
    private async Task CloseModal()
    {
        // Editing in progress — closing now would silently throw away whatever was typed,
        // so confirm first instead of assuming a stray backdrop tap meant "discard this."
        if (IsEditMode)
        {
            var confirm = new ConfirmationPopup(
                "Discard Changes?",
                "You have unsaved changes to this tooth. Discard them?",
                "Discard",
                Color.FromArgb("#D32F2F"));

            var confirmed = await Shell.Current.CurrentPage.ShowPopupAsync(confirm);
            if (confirmed is not true)
                return;
        }

        IsModalVisible = false;
        IsEditMode = false;

        if (_modalTooth != null)
            _modalTooth.IsSelected = false;

        _modalTooth = null;
    }

    // ═══════════════════════════════════════════════════════════════
    // ENTER EDIT MODE
    // ═══════════════════════════════════════════════════════════════

    [RelayCommand]
    private void EnterEditMode()
    {
        if (_modalTooth == null)
            return;

        EditCondition = _modalTooth.Condition;
        EditNotes = _modalTooth.Notes;

        IsEditMode = true;
    }

    // ═══════════════════════════════════════════════════════════════
    // CANCEL EDIT
    // ═══════════════════════════════════════════════════════════════

    [RelayCommand]
    private void CancelEdit()
    {
        if (_modalTooth != null)
        {
            EditCondition =
                _modalTooth.Condition;

            EditNotes =
                _modalTooth.Notes;
        }

        IsEditMode = false;
    }

    // ═══════════════════════════════════════════════════════════════
    // SAVE EDIT
    // ═══════════════════════════════════════════════════════════════

    [RelayCommand]
    private async Task SaveEditAsync()
    {
        if (_modalTooth == null || IsBusy)
            return;

        var confirm = new ConfirmationPopup(
            "Save Changes?",
            $"Save this condition for Tooth #{_modalTooth.ToothNumber}?",
            "Save",
            Color.FromArgb("#2E7D32"));

        var confirmed = await Shell.Current.CurrentPage.ShowPopupAsync(confirm);
        if (confirmed is not true)
            return;

        IsBusy = true;

        try
        {
            if (string.IsNullOrWhiteSpace(EditCondition))
                EditCondition = "Normal";

            if (!ConditionColors.TryGetValue(
                    EditCondition,
                    out var hex))
            {
                hex = "#FFFFFF";
            }

            var previousCondition =
                _modalTooth.Condition;

            var isNew =
                previousCondition.Equals(
                    "Normal",
                    StringComparison.OrdinalIgnoreCase)
                &&
                string.IsNullOrWhiteSpace(
                    _modalTooth.LastUpdated);

            var now =
                DateTime.UtcNow;

            // -------------------------------------------------------
            // Save current tooth state
            // -------------------------------------------------------

            var record = new ToothRecord
            {
                PatientId = PatientId,
                ToothNumber = _modalTooth.ToothNumber,
                Condition = EditCondition,
                Color = hex,
                Notes = EditNotes ?? string.Empty,
                LastUpdated = now.ToString("yyyy-MM-dd"),
                DateUpdated = now.ToString("yyyy-MM-dd")
            };

            await _db.SaveToothRecord(record);

            // -------------------------------------------------------
            // Update UI
            // -------------------------------------------------------

            _modalTooth.ApplyRecord(record);
            RefreshConditionCount();

            // -------------------------------------------------------
            // Add treatment history
            // -------------------------------------------------------

            var historyEntry =
                new TreatmentHistory
                {
                    PatientId = PatientId,
                    ToothNumber =
                        _modalTooth.ToothNumber,

                    ToothName =
                        _modalTooth.ToothName,

                    Condition =
                        EditCondition,

                    PreviousCondition =
                        previousCondition,

                    Color =
                        hex,

                    Notes =
                        EditNotes ?? string.Empty,

                    ActionType =
                        isNew
                            ? "Added"
                            : "Updated",

                    Timestamp =
                        DateTime.Now.ToString(
                            "yyyy-MM-dd HH:mm:ss"),

                    Description =
                        EditCondition
                };

            await _db.AddTreatmentHistory(
                historyEntry);

            // -------------------------------------------------------
            // Refresh modal
            // -------------------------------------------------------

            ModalCondition =
                EditCondition;

            ModalConditionColor =
                Color.FromArgb(hex);

            ModalLastUpdated =
                now.ToLocalTime()
                   .ToString("MMM dd, yyyy");

            ModalNotes =
                EditNotes ?? string.Empty;

            IsEditMode = false;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine(
                $"[DentalChart] Save error: {ex}");

            await ShowNoticeAsync("Unable to Save",
                "Something went wrong saving this tooth's condition. Please try again.");
        }
        finally
        {
            IsBusy = false;
        }
    }

    // ═══════════════════════════════════════════════════════════════
    // CLEAR TOOTH
    // ═══════════════════════════════════════════════════════════════

    [RelayCommand]
    private async Task ClearToothFromModalAsync()
    {
        if (_modalTooth == null || IsBusy)
            return;

        // Destructive and permanent — confirm before wiping the tooth's recorded condition.
        var confirm = new ConfirmationPopup(
            "Clear Tooth Condition?",
            $"This will remove Tooth #{_modalTooth.ToothNumber}'s recorded condition. This can't be undone.",
            "Clear",
            Color.FromArgb("#D32F2F"));

        var confirmed = await Shell.Current.CurrentPage.ShowPopupAsync(confirm);
        if (confirmed is not true)
            return;

        IsBusy = true;

        try
        {
            int toothNumber =
                _modalTooth.ToothNumber;

            var previousCondition =
                _modalTooth.Condition;

            var previousColor =
                ConditionColors.TryGetValue(
                    previousCondition,
                    out var color)
                    ? color
                    : "#FFFFFF";

            // -------------------------------------------------------
            // Record clearing in treatment history
            // -------------------------------------------------------

            if (!previousCondition.Equals(
                    "Normal",
                    StringComparison.OrdinalIgnoreCase))
            {
                var historyEntry =
                    new TreatmentHistory
                    {
                        PatientId = PatientId,
                        ToothNumber = toothNumber,
                        ToothName = _modalTooth.ToothName,

                        Condition = "Normal",

                        PreviousCondition =
                            previousCondition,

                        Color =
                            previousColor,

                        Notes = string.Empty,

                        ActionType = "Cleared",

                        Timestamp =
                            DateTime.Now.ToString(
                                "yyyy-MM-dd HH:mm:ss"),

                        Description =
                            "Tooth condition cleared"
                    };

                await _db.AddTreatmentHistory(
                    historyEntry);
            }

            // -------------------------------------------------------
            // Delete current tooth record
            // -------------------------------------------------------

            await _db.DeleteToothRecord(
                PatientId,
                toothNumber);

            _modalTooth.Reset();
            RefreshConditionCount();

            await CloseModal();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine(
                $"[DentalChart] Clear error: {ex}");

            await ShowNoticeAsync("Unable to Clear",
                "Something went wrong clearing this tooth's condition. Please try again.");
        }
        finally
        {
            IsBusy = false;
        }
    }

    // ═══════════════════════════════════════════════════════════════
    // LEGACY RESET
    // ═══════════════════════════════════════════════════════════════

    [RelayCommand]
    private async Task ResetTooth(int toothNumber)
    {
        if (IsBusy)
            return;

        IsBusy = true;

        try
        {
            var tooth =
                _allTeeth.FirstOrDefault(
                    t => t.ToothNumber == toothNumber);

            tooth?.Reset();

            await _db.DeleteToothRecord(
                PatientId,
                toothNumber);
        }
        finally
        {
            IsBusy = false;
        }
    }
}
