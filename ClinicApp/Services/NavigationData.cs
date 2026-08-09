namespace ClinicApp.Services;

/// <summary>
/// Temporary storage for passing data between pages during navigation
/// </summary>
public static class NavigationData
{
    public static List<Landmark>? PendingLandmarks { get; set; }
    public static int PendingPatientId { get; set; }
    public static string? PendingPatientName { get; set; }
}