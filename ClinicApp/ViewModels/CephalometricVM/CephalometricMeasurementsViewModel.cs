using ClinicApp.Config;
using ClinicApp.Models;
using ClinicApp.Models.PatientModels;
using ClinicApp.Services;
using ClinicApp.Services.CephaTrain;
using ClinicApp.Views.Shared;
using CommunityToolkit.Maui.Views;
using CommunityToolkit.Mvvm.ComponentModel;
using System.Text.Json;

namespace ClinicApp.ViewModels.CephalometricVM;

public partial class CephalometricMeasurementsViewModel : ObservableObject
{
    readonly DatabaseService _db;

    [ObservableProperty] List<MeasurementResultViewModel> measurements = new();
    [ObservableProperty] string? patientName;
    [ObservableProperty] DateTime measurementDate = DateTime.Now;

    public CephalometricMeasurementsViewModel(DatabaseService db)
    {
        _db = db;

        if (NavigationData.PendingLandmarks != null && NavigationData.PendingLandmarks.Count > 0)
        {
            PatientName = NavigationData.PendingPatientName;

            _ = CalculateFromLandmarks(
                NavigationData.PendingPatientId,
                NavigationData.PendingPatientName ?? "",
                NavigationData.PendingLandmarks,
                NavigationData.PendingPixelsPerMm);

            NavigationData.PendingLandmarks = null;
        }
    }

    public async Task CalculateFromLandmarks(int patientId, string patientName, List<Landmark> landmarks, float pixelsPerMm)
    {
        try
        {
            System.Diagnostics.Debug.WriteLine($"📊 CalculateFromLandmarks called");
            System.Diagnostics.Debug.WriteLine($"   PatientID: {patientId}");
            System.Diagnostics.Debug.WriteLine($"   PatientName: {patientName}");
            System.Diagnostics.Debug.WriteLine($"   Landmarks count: {landmarks?.Count ?? 0}");

            if (landmarks == null || landmarks.Count == 0)
            {
                System.Diagnostics.Debug.WriteLine("❌ Landmarks list is null or empty!");
                await Shell.Current.CurrentPage.ShowPopupAsync(new ConfirmationPopup(
                    "No Landmarks", "No landmarks to calculate from", "OK", PopupAction.Positive, showCancelButton: false));
                return;
            }

            PatientName = patientName;

            // Debug: print first 3 landmarks
            for (int i = 0; i < Math.Min(3, landmarks.Count); i++)
            {
                var l = landmarks[i];
                System.Diagnostics.Debug.WriteLine($"   Landmark {i}: {l.ClassName} at ({l.X:F1}, {l.Y:F1})");
            }

            // Calculate all measurements
            System.Diagnostics.Debug.WriteLine("🧮 Calling CephalometricCalculations.CalculateMeasurements...");
            var values = CephalometricCalculations.CalculateMeasurements(landmarks, pixelsPerMm);



            System.Diagnostics.Debug.WriteLine($"📈 Calculations returned {values.Count} measurements");

            if (values.Count == 0)
            {
                System.Diagnostics.Debug.WriteLine("❌ No measurements calculated!");
                await Shell.Current.CurrentPage.ShowPopupAsync(new ConfirmationPopup(
                    "No Measurements", "Could not calculate measurements", "OK", PopupAction.Positive, showCancelButton: false));
                return;
            }

            // Debug: print calculated values
            foreach (var (name, value) in values.Take(5))
            {
                System.Diagnostics.Debug.WriteLine($"   {name}: {value:F2}");
            }

            // Convert to view model format
            var resultsList = new List<MeasurementResultViewModel>();
            foreach (var (name, value) in values)
            {
                var range = NormalRanges.GetRange(name);
                if (range == null)
                {
                    System.Diagnostics.Debug.WriteLine($"⚠️  No normal range found for: {name}");
                    continue;
                }

                var status = NormalRanges.CheckStatus(name, value);
                resultsList.Add(new MeasurementResultViewModel
                {
                    Name = range.Name,
                    Value = Math.Round(value, 1),
                    Min = range.Min,
                    Max = range.Max,
                    Unit = range.Unit,
                    Status = status,
                    Description = range.Description
                });
            }

            System.Diagnostics.Debug.WriteLine($"✅ Created {resultsList.Count} measurement results");
            Measurements = resultsList;

            // Save to database
            await SaveMeasurements(patientId, values, landmarks, pixelsPerMm);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"❌ CalculateFromLandmarks error: {ex.Message}");
            System.Diagnostics.Debug.WriteLine($"   StackTrace: {ex.StackTrace}");
            await Shell.Current.CurrentPage.ShowPopupAsync(new ConfirmationPopup(
                "Error", $"Calculation failed: {ex.Message}", "OK", PopupAction.Positive, showCancelButton: false));
        }
    }

    private async Task SaveMeasurements(int patientId, Dictionary<string, double> values, List<Landmark> landmarks, float pixelsPerMm)
    {
        var manuallyPlacedNames = landmarks.Where(l => l.IsManuallyPlaced).Select(l => l.ClassName).ToList();
        var notes = new List<string>();

        if (manuallyPlacedNames.Count > 0)
            notes.Add($"Includes manually placed landmarks: {string.Join(", ", manuallyPlacedNames)}");

        notes.Add(pixelsPerMm > 0
            ? $"Ruler calibration: {pixelsPerMm:F2} px/mm"
            : "Not calibrated — linear measurements (AFH, PFH) unavailable");

        var measurement = new CephalometricMeasurement
        {
            PatientId = patientId,
            MeasurementDate = DateTime.Now,
            SNA_Angle = values.ContainsKey("SNA") ? values["SNA"] : null,
            SNB_Angle = values.ContainsKey("SNB") ? values["SNB"] : null,
            ANB_Angle = values.ContainsKey("ANB") ? values["ANB"] : null,
            FMA = values.ContainsKey("FMA") ? values["FMA"] : null,
            SN_GoGn = values.ContainsKey("SN_GoGn") ? values["SN_GoGn"] : null,
            U1_SN = values.ContainsKey("U1_SN") ? values["U1_SN"] : null,
            L1_MP = values.ContainsKey("L1_MP") ? values["L1_MP"] : null,
            LandmarkData = JsonSerializer.Serialize(values),
            Notes = string.Join(" | ", notes)
        };

        await _db.SaveCephalometricMeasurement(measurement);
    }
}

public class MeasurementResultViewModel : ObservableObject
{
    public string Name { get; set; } = "";
    public double Value { get; set; }
    public double Min { get; set; }
    public double Max { get; set; }
    public string Unit { get; set; } = "°";
    public string Status { get; set; } = "Normal";
    public string Description { get; set; } = "";

    public Color StatusColor => Status switch
    {
        "Normal" => Colors.Green,
        "High" => Colors.Orange,
        "Low" => Colors.Blue,
        _ => Colors.Gray
    };

    // Light tint of StatusColor, for the badge background — computed here instead of via a converter, since none was registered for this.
    public Color StatusBgColor => Status switch
    {
        "Normal" => Color.FromArgb("#E8F5E9"),
        "High" => Color.FromArgb("#FFF3E0"),
        "Low" => Color.FromArgb("#E3F2FD"),
        _ => Color.FromArgb("#F5F5F5")
    };

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
