using ClinicApp.Config;
using ClinicApp.Models.PatientModels;
using ClinicApp.Services;
using ClinicApp.Services.CephaTrain;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace ClinicApp.ViewModels.CephalometricVM;

[QueryProperty(nameof(PatientId), "PatientId")]
[QueryProperty(nameof(PatientName), "PatientName")]
public partial class CephalometricViewModel : ObservableObject
{
    readonly DatabaseService _db;
    private OnDeviceCephalometricDetector? _detector;


    public CephalometricViewModel(DatabaseService db, OnDeviceCephalometricDetector detector)
    {
        _db = db;
        _detector = detector;
        _ = _detector.InitializeAsync();   // warm the model up while the user picks an image
    }
    public bool ShowMissingLandmarksUI => HasLandmarks && MissingLandmarkNames.Count > 0;

    [ObservableProperty] int patientId;
    [ObservableProperty] string? patientName;
    [ObservableProperty] string? imagePath;
    [ObservableProperty] bool hasImage;
    [ObservableProperty] bool isAnalyzing;
    [ObservableProperty] List<Landmark> detectedLandmarks = new();
    [ObservableProperty] List<OutlinePoint> softTissueOutline = new();
    [ObservableProperty] List<string> incompletePlanes = new();
    [ObservableProperty] string incompletePlanesMessage = "";
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowMissingLandmarksUI))]
    List<string> missingLandmarkNames = new();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowMissingLandmarksUI))]
    bool hasLandmarks;

    [ObservableProperty] string? landmarkBeingPlaced = null;  // null = not in placement mode

    // In CephalometricViewModel.cs

    [ObservableProperty] float pixelsPerMm = 0f;              // 0 = uncalibrated
    [ObservableProperty] bool isCalibrated = false;
    [ObservableProperty] string calibrationStatusMessage = "";
    [ObservableProperty] bool isCalibrating = false;           // true while placing the two ruler taps
    [ObservableProperty] float? calibrationPointAX = null;
    [ObservableProperty] float? calibrationPointAY = null;

    private double? _autoDetectedPixelsPerMm;
    private double? _autoDetectedConfidence;

    // Called from AnalyzeImage() after a successful detection
    private void ApplyRulerDetection(double? pixelsPerMm, double? confidence)
    {
        _autoDetectedPixelsPerMm = pixelsPerMm;
        _autoDetectedConfidence = confidence;

        if (pixelsPerMm.HasValue && confidence >= 0.5)
        {
            // Pre-fill from auto-detection, but still requires explicit confirmation
            PixelsPerMm = (float)pixelsPerMm.Value;
            IsCalibrated = false;
            CalibrationStatusMessage = $"📏 Ruler auto-detected (confidence {confidence:P0}) — tap Confirm Scale to verify, or recalibrate manually.";
        }
        else
        {
            PixelsPerMm = 0f;
            IsCalibrated = false;
            CalibrationStatusMessage = "📏 Ruler not confidently detected — tap two points a known distance apart on the ruler to calibrate manually.";
        }
    }

    [RelayCommand]
    void StartManualCalibration()
    {
        IsCalibrating = true;
        CalibrationPointAX = null;
        CalibrationPointAY = null;
        CalibrationStatusMessage = "📏 Tap the FIRST ruler mark.";
    }

    [RelayCommand]
    void CancelCalibration()
    {
        IsCalibrating = false;
        CalibrationPointAX = null;
        CalibrationPointAY = null;
        CalibrationStatusMessage = IsCalibrated
            ? $"📏 Calibrated: {PixelsPerMm:F2} px/mm"
            : "📏 Not calibrated — linear measurements (AFH, PFH) unavailable.";
    }

    // Called from code-behind on each tap while IsCalibrating is true
    public async void HandleCalibrationTap(float x, float y)
    {
        if (!IsCalibrating) return;

        if (CalibrationPointAX == null)
        {
            // First tap — just record it, wait for the second
            CalibrationPointAX = x;
            CalibrationPointAY = y;
            CalibrationStatusMessage = "📏 Tap the SECOND ruler mark (the known distance from the first).";
            return;
        }

        // Second tap — ask for the real-world distance between the two taps
        string? input = await Shell.Current.DisplayPromptAsync(
            "Calibration Distance",
            "Enter the real-world distance between the two points you tapped (in millimeters). Check the ruler markings on the X-ray.",
            "OK", "Cancel",
            placeholder: "e.g. 10",
            keyboard: Keyboard.Numeric);

        if (string.IsNullOrWhiteSpace(input) || !float.TryParse(input, out float knownMm) || knownMm <= 0)
        {
            CalibrationStatusMessage = "⚠ Calibration cancelled — invalid distance entered.";
            IsCalibrating = false;
            CalibrationPointAX = null;
            CalibrationPointAY = null;
            return;
        }

        float pixelDistance = MathF.Sqrt(MathF.Pow(x - CalibrationPointAX.Value, 2) + MathF.Pow(y - CalibrationPointAY.Value, 2));
        if (pixelDistance <= 0)
        {
            CalibrationStatusMessage = "⚠ The two points were too close together — try again with points further apart.";
            IsCalibrating = false;
            CalibrationPointAX = null;
            CalibrationPointAY = null;
            return;
        }

        PixelsPerMm = pixelDistance / knownMm;
        IsCalibrated = true;
        IsCalibrating = false;
        CalibrationPointAX = null;
        CalibrationPointAY = null;
        CalibrationStatusMessage = $"✅ Calibrated: {PixelsPerMm:F2} px/mm (from {knownMm}mm reference)";
    }

    [RelayCommand]
    void ConfirmAutoDetectedScale()
    {
        if (PixelsPerMm <= 0) return;
        IsCalibrated = true;
        CalibrationStatusMessage = $"✅ Confirmed: {PixelsPerMm:F2} px/mm";
    }
    partial void OnPatientIdChanged(int value)
    {
        if (value > 0)
            LoadImage(value);
    }



    private async void LoadImage(int patientId)
    {
        var record = await _db.GetActiveCephalometricImage(patientId);
        if (record != null && File.Exists(record.FilePath))
        {
            ImagePath = record.FilePath;
            HasImage = true;
        }
        else
        {
            ImagePath = null;
            HasImage = false;
        }
    }

    [RelayCommand]
    async Task UploadImage()
    {
        await PickAndSaveImage();
    }

    [RelayCommand]
    async Task ReplaceImage()
    {
        bool confirm = await Shell.Current.DisplayAlert(
            "Replace Image",
            "Are you sure you want to replace the current X-ray image? The old image will be archived.",
            "Yes", "Cancel");

        if (confirm)
            await PickAndSaveImage();
    }

    [RelayCommand]
    async Task AnalyzeImage()
    {
        if (string.IsNullOrEmpty(ImagePath) || !File.Exists(ImagePath))
        {
            await Shell.Current.DisplayAlert("Error", "No image loaded.", "OK");
            return;
        }

        try
        {
            IsAnalyzing = true;

            System.Diagnostics.Debug.WriteLine("📤 Running on-device detection...");
            var result = await _detector.DetectLandmarksAsync(ImagePath);
            var landmarks = result.Landmarks;

            System.Diagnostics.Debug.WriteLine($"📊 Detected {landmarks.Count} landmarks, {result.SoftTissueOutline.Count} outline points");

            if (landmarks.Count == 0)
            {
                await Shell.Current.DisplayAlert("No Landmarks", "No landmarks detected.", "OK");
                DetectedLandmarks = new();
                SoftTissueOutline = new();
                MissingLandmarkNames = new();
                IncompletePlanes = new();
                IncompletePlanesMessage = "";
                LandmarkBeingPlaced = null;
                PixelsPerMm = 0f;
                IsCalibrated = false;
                IsCalibrating = false;
                CalibrationStatusMessage = "";
                HasLandmarks = false;
                return;
            }

            DetectedLandmarks = landmarks;
            SoftTissueOutline = result.SoftTissueOutline;
            IncompletePlanes = result.IncompletePlanes;
            MissingLandmarkNames = result.MissingLandmarks;
            IncompletePlanesMessage = result.IncompletePlanes.Count > 0
                ? $"⚠ {string.Join(", ", result.IncompletePlanes)} not shown — tap a missing landmark below to add it manually."
                : "";

            ApplyRulerDetection(result.PixelsPerMm, result.RulerConfidence);

            HasLandmarks = true;

            NavigationData.PendingLandmarks = landmarks;
            NavigationData.PendingPatientId = PatientId;
            NavigationData.PendingPatientName = PatientName;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"❌ Error: {ex.Message}");
            await Shell.Current.DisplayAlert("Error", $"Analysis failed: {ex.Message}", "OK");
        }
        finally
        {
            IsAnalyzing = false;
        }
    }

    [RelayCommand]
    async Task ConfirmMeasurements()
    {
        try
        {
            if (!HasLandmarks || DetectedLandmarks.Count == 0)
            {
                await Shell.Current.DisplayAlert("No Landmarks", "Analyze an image first.", "OK");
                return;
            }

            if (!IsCalibrated)
            {
                bool proceedAnyway = await Shell.Current.DisplayAlert(
                    "Not Calibrated",
                    "The ruler scale hasn't been confirmed. Linear measurements (AFH, PFH) will be skipped — only angle-based measurements (SNA, SNB, ANB, FMA, SN-GoGn) will be calculated. Continue?",
                    "Continue", "Calibrate First");

                if (!proceedAnyway) return;
            }

            NavigationData.PendingLandmarks = DetectedLandmarks;
            NavigationData.PendingPixelsPerMm = IsCalibrated ? PixelsPerMm : 0f;
            NavigationData.PendingPatientId = PatientId;
            NavigationData.PendingPatientName = PatientName;

            await Shell.Current.GoToAsync("measurements");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"❌ ConfirmMeasurements crashed: {ex}");
            await Shell.Current.DisplayAlert("Error", $"Could not open measurements: {ex.Message}", "OK");
        }
    }

    private async Task PickAndSaveImage()
    {
        try
        {
            var result = await MediaPicker.PickPhotoAsync(new MediaPickerOptions
            {
                Title = "Select Lateral Cephalometric X-ray"
            });

            if (result == null) return;

            string fileName = $"cepha_{PatientId}_{DateTime.Now:yyyyMMddHHmmss}{Path.GetExtension(result.FileName)}";
            string destPath = Path.Combine(FileSystem.AppDataDirectory, fileName);

            using var sourceStream = await result.OpenReadAsync();
            using var destStream = File.OpenWrite(destPath);
            await sourceStream.CopyToAsync(destStream);

            var newRecord = new CephalometricImage
            {
                PatientId = PatientId,
                FilePath = destPath
            };
            await _db.SaveCephalometricImage(newRecord);

            ImagePath = destPath;
            HasImage = true;

            // Clear EVERYTHING derived from the previous image's analysis —
            // otherwise stale chips/warnings from the old X-ray linger until
            // the new one is analyzed.
            DetectedLandmarks = new();
            SoftTissueOutline = new();
            MissingLandmarkNames = new();
            IncompletePlanes = new();
            IncompletePlanesMessage = "";
            LandmarkBeingPlaced = null;
            HasLandmarks = false;
            PixelsPerMm = 0f;
            IsCalibrated = false;
            IsCalibrating = false;
            CalibrationPointAX = null;
            CalibrationPointAY = null;
            CalibrationStatusMessage = "";
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Image pick error: {ex.Message}");
            await Shell.Current.DisplayAlert("Error", "Could not load the image. Please try again.", "OK");
        }
    }

    [RelayCommand]
    void StartPlacingLandmark(string landmarkName)
    {
        LandmarkBeingPlaced = LandmarkBeingPlaced == landmarkName ? null : landmarkName;

    }

    // In CephalometricViewModel
    public async void PlaceLandmarkAt(string className, float x, float y, int imageWidth, int imageHeight)
    {
        int classId = ApiConfig.LandmarkClassOrder.IndexOf(className);
        if (classId < 0) return;

        var region = LandmarkRegionGuide.GetRegion(className);
        if (region != null && imageWidth > 0 && imageHeight > 0)
        {
            float normX = x / imageWidth;
            float normY = y / imageHeight;
            float dx = normX - region.CenterX;
            float dy = normY - region.CenterY;
            float distance = MathF.Sqrt(dx * dx + dy * dy);

            // Allow some slack beyond the drawn guide circle before warning —
            // real anatomy varies, this shouldn't feel like a strict cage
            float warnThreshold = region.RadiusFraction * 2.5f;

            if (distance > warnThreshold)
            {
                bool proceedAnyway = await Shell.Current.DisplayAlert(
                    "Unusual Position",
                    $"This placement is further from where {className} is typically found than expected. " +
                    "This can happen with unusual anatomy or a tightly cropped X-ray — but double-check before continuing.",
                    "Place Anyway", "Cancel");

                if (!proceedAnyway)
                {
                    LandmarkBeingPlaced = null;
                    return;
                }

            }

        }

        var newLandmark = new Landmark
        {
            X = x,
            Y = y,
            ClassId = classId,
            ClassName = className,
            Confidence = 0f,
            IsManuallyPlaced = true,
            Index = DetectedLandmarks.Count + 1
        };

        var updated = new List<Landmark>(DetectedLandmarks) { newLandmark };
        DetectedLandmarks = updated;

        MissingLandmarkNames = MissingLandmarkNames.Where(n => n != className).ToList();
        LandmarkBeingPlaced = null;

        RecomputeIncompletePlanes();
    }

    private void RecomputeIncompletePlanes()
    {
        var detected = DetectedLandmarks.Select(l => l.ClassName).ToHashSet();
        var planeDeps = new Dictionary<string, string[]>
        {
            ["S-N line"] = new[] { "Sella", "Nasion" },
            ["N-A line"] = new[] { "Nasion", "Subspinale" },
            ["N-B line"] = new[] { "Nasion", "Supramentale" },
            ["Frankfort plane"] = new[] { "Porion", "Orbitale" },
            ["Mandibular plane"] = new[] { "Gonion", "Menton" },
        };

        IncompletePlanes = planeDeps
            .Where(kv => kv.Value.Any(req => !detected.Contains(req)))
            .Select(kv => kv.Key)
            .ToList();

        IncompletePlanesMessage = IncompletePlanes.Count > 0
            ? $"⚠ {string.Join(", ", IncompletePlanes)} not shown — tap a missing landmark below to add it manually."
            : "";
    }
}