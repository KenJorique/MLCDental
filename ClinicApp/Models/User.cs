using SQLite;

namespace ClinicApp.Models;

public class User
{
    [PrimaryKey, AutoIncrement]
    public int UserID { get; set; }

    public string? FullName { get; set; }

    public string? Username { get; set; }

    public string? Password { get; set; }

    public string? Role { get; set; }

    public string? ContactNo { get; set; }

    public string? Email { get; set; }

    // True = Active, False = Inactive. Defaults to true for new users.
    public bool IsActive { get; set; } = true;
}
