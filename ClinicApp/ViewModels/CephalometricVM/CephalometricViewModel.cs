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

    public CephalometricViewModel(DatabaseService db)
    {
        _db = db;
    }


    [ObservableProperty] int patientId;
    [ObservableProperty] string? patientName;
    [ObservableProperty] string? imagePath;
    // True when an image has been uploaded — controls which UI is shown
    [ObservableProperty] bool hasImage;

    // Automatically load image when PatientId is set via navigation
    partial void OnPatientIdChanged(int value)
    {
        if (value > 0)
            LoadImage(value);
    }

    // Loads the active cephalometric image for this patient from the database
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

    // Opens the image picker to upload a new X-ray image
    [RelayCommand]
    async Task UploadImage()
    {
        await PickAndSaveImage();
    }

    // Asks for confirmation then opens image picker to replace the current image
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

    // Analyze Image is disabled for now — placeholder command
    [RelayCommand]
    async Task AnalyzeImage()
    {
        await Shell.Current.DisplayAlert("Coming Soon", "Image analysis is not available yet.", "OK");
    }

    // ─── Helpers ──────────────────────────────────────────────

    // Opens the file picker (JPEG/PNG only), copies file to app storage, saves path to DB
    private async Task PickAndSaveImage()
    {
        try
        {
            // Open image picker limited to JPEG and PNG
            var result = await MediaPicker.PickPhotoAsync(new MediaPickerOptions
            {
                Title = "Select Lateral Cephalometric X-ray"
            });

            if (result == null) return;

            // Copy the image to app's local data directory for persistent storage
            string fileName = $"cepha_{PatientId}_{DateTime.Now:yyyyMMddHHmmss}{Path.GetExtension(result.FileName)}";
            string destPath = Path.Combine(FileSystem.AppDataDirectory, fileName);

            using var sourceStream = await result.OpenReadAsync();
            using var destStream = File.OpenWrite(destPath);
            await sourceStream.CopyToAsync(destStream);

            // Save the file path to the database (archives the old image automatically)
            var newRecord = new CephalometricImage
            {
                PatientId = PatientId,
                FilePath = destPath
            };
            await _db.SaveCephalometricImage(newRecord);

            // Update the UI
            ImagePath = destPath;
            HasImage = true;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Image pick error: {ex.Message}");
            await Shell.Current.DisplayAlert("Error", "Could not load the image. Please try again.", "OK");
        }
    }
}
