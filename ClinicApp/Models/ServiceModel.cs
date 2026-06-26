using SQLite;

namespace ClinicApp.Models;

public class ServiceModel
{
    [PrimaryKey, AutoIncrement]
    public int ServiceID { get; set; }

    public string ServiceName { get; set; }

    public double Price { get; set; }

    public string Description { get; set; }

    // Soft delete — hidden from list but kept in DB
    public bool IsDeleted { get; set; } = false;
}
