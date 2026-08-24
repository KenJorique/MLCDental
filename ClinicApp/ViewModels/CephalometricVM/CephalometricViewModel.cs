using ClinicApp.Config;
using ClinicApp.Models.PatientModels;
using ClinicApp.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace ClinicApp.ViewModels.CephalometricVM;

[QueryProperty(nameof(PatientId), "PatientId")]
[QueryProperty(nameof(PatientName), "PatientName")]
public partial class CephalometricViewModel : ObservableObject
{
    readonly DatabaseService _db;
    private CephalometricLandmarkDetector? _detector;

    public CephalometricViewModel(DatabaseService db)
    {
        _db = db;
        InitializeDetector();
    }

    [ObservableProperty] int patientId;
    [ObservableProperty] string? patientName;
    [ObservableProperty] string? imagePath;
    [ObservableProperty] bool hasImage;
    [ObservableProperty] bool isAnalyzing;
    [ObservableProperty] List<Landmark> detectedLandmarks = new();
    [ObservableProperty] bool hasLandmarks;
    [ObservableProperty] List<OutlinePoint> softTissueOutline = new();
    [ObservableProperty] List<string> incompletePlanes = new();
    [ObservableProperty] string incompletePlanesMessage = "";

    partial void OnPatientIdChanged(int value)
    {
        if (value > 0)
            LoadImage(value);
    }

    private void InitializeDetector()
    {
        try
        {
            _detector = new CephalometricLandmarkDetector(ApiConfig.CephalometricApiUrl);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Detector init error: {ex.Message}");
        }
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

        if (_detector == null)
        {
            await Shell.Current.DisplayAlert("Error", "Detector not initialized.", "OK");
            return;
        }

        try
        {
            IsAnalyzing = true;

            System.Diagnostics.Debug.WriteLine("🔍 Testing connection...");
            bool isConnected = await _detector.TestConnectionAsync();
            if (!isConnected)
            {
                await Shell.Current.DisplayAlert(
                    "Server Not Reachable",
                    "Could not connect to analysis server.",
                    "OK");
                return;
            }

            System.Diagnostics.Debug.WriteLine("📤 Running detection...");
            var result = await _detector.DetectLandmarksAsync(ImagePath);
            var landmarks = result.Landmarks;


            for (int i = 0; i < landmarks.Count; i++)
                landmarks[i].Index = i + 1;

            System.Diagnostics.Debug.WriteLine($"📊 Detected {landmarks.Count} landmarks, {result.SoftTissueOutline.Count} outline points");

            if (landmarks.Count == 0)
            {
                await Shell.Current.DisplayAlert("No Landmarks", "No landmarks detected.", "OK");
                DetectedLandmarks = new();
                SoftTissueOutline = new();
                IncompletePlanes = new();
                IncompletePlanesMessage = "";
                HasLandmarks = false;
                return;
            }

            DetectedLandmarks = landmarks;
            SoftTissueOutline = result.SoftTissueOutline;
            IncompletePlanes = result.IncompletePlanes;
            IncompletePlanesMessage = result.IncompletePlanes.Count > 0
                ? $"⚠ {string.Join(", ", result.IncompletePlanes)} not shown — one or more required landmarks weren't confidently detected."
                : "";
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

            NavigationData.PendingLandmarks = DetectedLandmarks;
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
            DetectedLandmarks = new();
            SoftTissueOutline = new();
            HasLandmarks = false;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Image pick error: {ex.Message}");
            await Shell.Current.DisplayAlert("Error", "Could not load the image. Please try again.", "OK");
        }
    }
}