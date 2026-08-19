using ClinicApp.Config;
using ClinicApp.Models;
using ClinicApp.Services;
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

        System.Diagnostics.Debug.WriteLine("🔍 MeasurementsViewModel initialized");

        // Load data from navigation
        if (NavigationData.PendingLandmarks != null && NavigationData.PendingLandmarks.Count > 0)
        {
            System.Diagnostics.Debug.WriteLine($"✅ Found {NavigationData.PendingLandmarks.Count} landmarks in NavigationData");

            PatientName = NavigationData.PendingPatientName;

            _ = CalculateFromLandmarks(
                NavigationData.PendingPatientId,
                NavigationData.PendingPatientName ?? "",
                NavigationData.PendingLandmarks);

            NavigationData.PendingLandmarks = null;
        }
        else
        {
            System.Diagnostics.Debug.WriteLine("❌ No landmarks found in NavigationData!");
        }
    }

    public async Task CalculateFromLandmarks(int patientId, string patientName, List<Landmark> landmarks)
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
                await Shell.Current.DisplayAlert("Error", "No landmarks to calculate from", "OK");
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
            var values = CephalometricCalculations.CalculateMeasurements(landmarks);

            System.Diagnostics.Debug.WriteLine($"📈 Calculations returned {values.Count} measurements");

            if (values.Count == 0)
            {
                System.Diagnostics.Debug.WriteLine("❌ No measurements calculated!");
                await Shell.Current.DisplayAlert("Error", "Could not calculate measurements", "OK");
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
            await SaveMeasurements(patientId, values);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"❌ CalculateFromLandmarks error: {ex.Message}");
            System.Diagnostics.Debug.WriteLine($"   StackTrace: {ex.StackTrace}");
            await Shell.Current.DisplayAlert("Error", $"Calculation failed: {ex.Message}", "OK");
        }
    }

    private async Task SaveMeasurements(int patientId, Dictionary<string, double> values)
    {
        try
        {
            System.Diagnostics.Debug.WriteLine($"💾 Saving measurements for patient {patientId}...");

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
                LandmarkData = JsonSerializer.Serialize(values)
            };

            await _db.SaveCephalometricMeasurement(measurement);
            System.Diagnostics.Debug.WriteLine("✅ Measurements saved to database");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"❌ Save error: {ex.Message}");
        }
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