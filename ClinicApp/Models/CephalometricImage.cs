using SQLite;

namespace ClinicApp.Models;

public class CephalometricImage
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    public int PatientId { get; set; }

    public string? FilePath { get; set; }

    // True = currently active/displayed image, False = archived (replaced)
    public bool IsActive { get; set; } = true;

    public string? UploadedDate { get; set; }
}
