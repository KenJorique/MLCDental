using ClinicApp.Config;
using ClinicApp.Models;
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

        if (_detector == null)
        {
            await Shell.Current.DisplayAlert("Error", "Detector not initialized.", "OK");
            return;
        }

        var urlInUse = ApiConfig.CephalometricApiUrl;
        await Shell.Current.DisplayAlert("Debug", $"Using URL:\n{urlInUse}", "OK");

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

            // Test connection first
            bool isConnected = await _detector.TestConnectionAsync();
            if (!isConnected)
            {
                await Shell.Current.DisplayAlert(
                    "Server Not Reachable",
                    "Could not connect to analysis server. Make sure:\n" +
                    "1. Python server is running (python server.py)\n" +
                    "2. IP address is correct in ApiConfig.cs\n" +
                    "3. Device and server are on same WiFi",
                    "OK");
                return;
            }

            // Run detection
            var landmarks = await _detector.DetectLandmarksAsync(ImagePath);

            if (landmarks.Count == 0)
            {
                await Shell.Current.DisplayAlert(
                    "No Landmarks",
                    "No landmarks detected. Try a clearer X-ray image.",
                    "OK");
                DetectedLandmarks.Clear();
                HasLandmarks = false;
            }
            else
            {
                DetectedLandmarks = landmarks;
                HasLandmarks = true;

                var summary = string.Join("\n", landmarks.Take(5).Select(l => l.ToString()));
                if (landmarks.Count > 5)
                    summary += $"\n... and {landmarks.Count - 5} more";

                await Shell.Current.DisplayAlert(
                    "Landmarks Detected",
                    $"Found {landmarks.Count} landmarks:\n\n{summary}",
                    "OK");
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Analysis error: {ex.Message}");
            await Shell.Current.DisplayAlert("Error", $"Analysis failed: {ex.Message}", "OK");
        }
        finally
        {
            IsAnalyzing = false;
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
            DetectedLandmarks.Clear();
            HasLandmarks = false;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Image pick error: {ex.Message}");
            await Shell.Current.DisplayAlert("Error", "Could not load the image. Please try again.", "OK");
        }
    }
}