using ClinicApp.Config;
using ClinicApp.Models.PatientModels;
using ClinicApp.Services;
using ClinicApp.Services.CephaTrain;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Globalization;
using System.Text.Json;

namespace ClinicApp.ViewModels.CephalometricVM;

public partial class CephalometricMeasurementsViewModel : ObservableObject
{
    readonly DatabaseService _db;

    int _patientId;
    float _pixelsPerMm;
    List<string> _manualLandmarkNames = new();
    CephalometricMeasurement? _savedRecord;

    [ObservableProperty] List<MeasurementResultViewModel> measurements = new();
    [ObservableProperty] string? patientName;
    [ObservableProperty] DateTime measurementDate = DateTime.Now;
    [ObservableProperty] string summaryText = "";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowEmptyState), nameof(CanSave))]
    bool hasMeasurements;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasInfoMessage))]
    string infoMessage = "";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanSave), nameof(SaveButtonText))]
    bool isSaved;

    public bool ShowEmptyState => !HasMeasurements;
    public bool HasInfoMessage => !string.IsNullOrEmpty(InfoMessage);
    public bool CanSave => HasMeasurements && !IsSaved;
    public string SaveButtonText => IsSaved ? "✓ Saved" : "Save Measurements";

    public CephalometricMeasurementsViewModel(DatabaseService db)
    {
        _db = db;
    }

    /// <summary>
    /// Called from the page's OnAppearing. Picks up the landmarks handed over by the
    /// analysis page. If nothing new is pending (e.g. returning to this page), the
    /// current results and any edits are left untouched.
    /// </summary>
    public async Task LoadPendingAsync()
    {
        var landmarks = NavigationData.PendingLandmarks;
        if (landmarks == null || landmarks.Count == 0) return;

        var patientId = NavigationData.PendingPatientId;
        var name = NavigationData.PendingPatientName ?? "";
        var pixelsPerMm = NavigationData.PendingPixelsPerMm;

        NavigationData.PendingLandmarks = null;

        await CalculateFromLandmarks(patientId, name, landmarks, pixelsPerMm);
    }

    public async Task CalculateFromLandmarks(int patientId, string patientName, List<Landmark> landmarks, float pixelsPerMm)
    {
        try
        {
            System.Diagnostics.Debug.WriteLine($"📊 CalculateFromLandmarks — patient {patientId}, {landmarks?.Count ?? 0} landmarks, {pixelsPerMm:F2} px/mm");

            if (landmarks == null || landmarks.Count == 0)
            {
                HasMeasurements = false;
                await Shell.Current.DisplayAlert("Error", "No landmarks to calculate from", "OK");
                return;
            }

            _patientId = patientId;
            _pixelsPerMm = pixelsPerMm;
            _manualLandmarkNames = landmarks
                .Where(l => l.IsManuallyPlaced)
                .Select(l => l.ClassName ?? "")
                .ToList();
            _savedRecord = null;

            PatientName = patientName;
            MeasurementDate = DateTime.Now;

            var values = CephalometricCalculations.CalculateMeasurements(landmarks, pixelsPerMm);
            System.Diagnostics.Debug.WriteLine($"📈 Calculations returned {values.Count} measurements");

            if (values.Count == 0)
            {
                Measurements = new();
                HasMeasurements = false;
                await Shell.Current.DisplayAlert("Error", "Could not calculate measurements", "OK");
                return;
            }

            var items = new List<MeasurementResultViewModel>();
            foreach (var (key, value) in values)
            {
                var range = NormalRanges.GetRange(key);
                if (range == null)
                {
                    System.Diagnostics.Debug.WriteLine($"⚠️  No normal range found for: {key}");
                    continue;
                }

                var item = new MeasurementResultViewModel
                {
                    Key = key,
                    Name = range.Name,
                    Min = range.Min,
                    Max = range.Max,
                    Unit = range.Unit,
                    Description = range.Description
                };
                item.InitializeValue(Math.Round(value, 1));
                items.Add(item);
            }

            Measurements = items;
            HasMeasurements = items.Count > 0;
            IsSaved = false;

            InfoMessage = pixelsPerMm > 0
                ? ""
                : "📏 Scale not calibrated — AFH and PFH (mm) were skipped. Go back and calibrate to include them.";

            RefreshSummary();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"❌ CalculateFromLandmarks error: {ex}");
            await Shell.Current.DisplayAlert("Error", $"Calculation failed: {ex.Message}", "OK");
        }
    }

    [RelayCommand]
    async Task EditMeasurement(MeasurementResultViewModel? item)
    {
        if (item == null) return;

        string? input = await Shell.Current.DisplayPromptAsync(
            $"Edit {item.Name}",
            $"Enter the corrected value ({item.Unit}).\nAuto-calculated: {item.OriginalValue:F1} {item.Unit}",
            accept: "Save",
            cancel: "Cancel",
            placeholder: item.OriginalValue.ToString("F1", CultureInfo.InvariantCulture),
            maxLength: 7,
            keyboard: Keyboard.Numeric,
            initialValue: item.Value.ToString("F1", CultureInfo.InvariantCulture));

        if (input == null) return; // cancelled

        input = input.Trim().Replace(',', '.');
        double maxAllowed = item.Unit == "°" ? 180 : 500;

        if (!double.TryParse(input, NumberStyles.Float, CultureInfo.InvariantCulture, out var newValue)
            || double.IsNaN(newValue) || double.IsInfinity(newValue)
            || newValue < 0 || newValue > maxAllowed)
        {
            await Shell.Current.DisplayAlert("Invalid value",
                $"Please enter a number between 0 and {maxAllowed:0} {item.Unit}.", "OK");
            return;
        }

        item.Value = Math.Round(newValue, 1);
        item.IsManualOverride = item.IsEdited;

        if (item.Key is "SNA" or "SNB") RecalculateAnb();
        AfterEdit();
    }

    [RelayCommand]
    void ResetMeasurement(MeasurementResultViewModel? item)
    {
        if (item == null) return;

        item.Value = item.OriginalValue;
        item.IsManualOverride = false;

        if (item.Key is "SNA" or "SNB") RecalculateAnb();
        AfterEdit();
    }

    [RelayCommand]
    async Task Save()
    {
        if (!CanSave) return;

        try
        {
            double? Get(string key) => Measurements.FirstOrDefault(m => m.Key == key)?.Value;

            var notes = new List<string>();
            if (_manualLandmarkNames.Count > 0)
                notes.Add($"Includes manually placed landmarks: {string.Join(", ", _manualLandmarkNames)}");

            notes.Add(_pixelsPerMm > 0
                ? $"Ruler calibration: {_pixelsPerMm:F2} px/mm"
                : "Not calibrated — linear measurements (AFH, PFH) unavailable");

            var edits = Measurements
                .Where(m => m.IsEdited)
                .Select(m => $"{m.Name} {m.OriginalValue:F1}→{m.Value:F1}")
                .ToList();
            if (edits.Count > 0)
                notes.Add($"Edited by clinician: {string.Join(", ", edits)}");

            _savedRecord ??= new CephalometricMeasurement();
            _savedRecord.PatientId = _patientId;
            _savedRecord.MeasurementDate = MeasurementDate;
            _savedRecord.SNA_Angle = Get("SNA");
            _savedRecord.SNB_Angle = Get("SNB");
            _savedRecord.ANB_Angle = Get("ANB");
            _savedRecord.FMA = Get("FMA");
            _savedRecord.SN_GoGn = Get("SN_GoGn");
            _savedRecord.IMPA = Get("IMPA");
            _savedRecord.U1_SN = Get("U1_SN");
            _savedRecord.L1_MP = Get("L1_MP");
            _savedRecord.LandmarkData = JsonSerializer.Serialize(
                Measurements.ToDictionary(m => m.Key, m => m.Value));
            _savedRecord.Notes = string.Join(" | ", notes);

            await _db.SaveCephalometricMeasurement(_savedRecord);

            IsSaved = true;
            await Shell.Current.DisplayAlert("Saved", "Measurements saved to the patient record.", "OK");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"❌ Save error: {ex}");
            await Shell.Current.DisplayAlert("Error", $"Could not save: {ex.Message}", "OK");
        }
    }

    // ANB is derived (SNA - SNB). Keep it consistent unless the dentist set ANB directly.
    void RecalculateAnb()
    {
        var sna = Find("SNA");
        var snb = Find("SNB");
        var anb = Find("ANB");
        if (sna == null || snb == null || anb == null || anb.IsManualOverride) return;

        anb.Value = (sna.IsEdited || snb.IsEdited)
            ? Math.Round(sna.Value - snb.Value, 1)
            : anb.OriginalValue;
    }

    MeasurementResultViewModel? Find(string key) => Measurements.FirstOrDefault(m => m.Key == key);

    void AfterEdit()
    {
        IsSaved = false; // edited since last save
        RefreshSummary();
    }

    void RefreshSummary()
    {
        int total = Measurements.Count;
        int normal = Measurements.Count(m => m.Status == "Normal");
        SummaryText = $"{normal} of {total} measurements within normal range";
    }
}

public class MeasurementResultViewModel : ObservableObject
{
    public string Key { get; set; } = "";
    public string Name { get; set; } = "";
    public double Min { get; set; }
    public double Max { get; set; }
    public string Unit { get; set; } = "°";
    public string Description { get; set; } = "";

    /// <summary>Value straight from the landmark calculation (before any edit).</summary>
    public double OriginalValue { get; private set; }

    /// <summary>True when the dentist typed this value in (as opposed to it changing because SNA/SNB changed).</summary>
    public bool IsManualOverride { get; set; }

    double _value;
    public double Value
    {
        get => _value;
        set
        {
            if (SetProperty(ref _value, value))
            {
                OnPropertyChanged(nameof(ValueText));
                OnPropertyChanged(nameof(Status));
                OnPropertyChanged(nameof(StatusColor));
                OnPropertyChanged(nameof(StatusBackground));
                OnPropertyChanged(nameof(NormalizedValue));
                OnPropertyChanged(nameof(IsEdited));
                OnPropertyChanged(nameof(EditedNote));
            }
        }
    }

    public void InitializeValue(double v)
    {
        OriginalValue = v;
        Value = v;
    }

    public bool IsEdited => Math.Abs(Value - OriginalValue) > 0.001;

    public string ValueText => Unit == "°" ? $"{Value:F1}°" : $"{Value:F1} {Unit}";

    public string NormalRangeText => $"Normal range: {Min:0.#} – {Max:0.#} {Unit}";

    public string EditedNote => IsEdited
        ? $"✏️ Edited — auto-calculated was {OriginalValue:F1} {Unit}"
        : "";

    public string Status => Value < Min ? "Low" : Value > Max ? "High" : "Normal";

    public Color StatusColor => Status switch
    {
        "Normal" => Colors.Green,
        "High" => Colors.Orange,
        "Low" => Colors.Blue,
        _ => Colors.Gray
    };

    public Color StatusBackground => StatusColor.WithAlpha(0.12f);

    public double NormalizedValue
    {
        get
        {
            double range = Max - Min;
            if (range == 0) return 0.5;
            return Math.Clamp((Value - Min) / range, 0, 1);
        }
    }
}