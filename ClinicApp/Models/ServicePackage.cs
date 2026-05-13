using SQLite;

namespace ClinicApp.Models;

public class ServicePackage
{
    [PrimaryKey, AutoIncrement]
    public int PackageID { get; set; }

    // Package display name
    public string? PackageName { get; set; }

    // Comma-separated list of included service names e.g. "Cleaning,Extraction"
    public string? IncludedServices { get; set; }

    public double Price { get; set; }

    public string? Description { get; set; }
}
