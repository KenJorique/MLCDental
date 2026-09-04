using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;

namespace ClinicApp.Models.SupabaseModels;

// Maps to the "users" table in Supabase — see users_table.sql for the DDL
// and required RLS policies. Mirrors the same [Table]/[PrimaryKey]/[Column]
// pattern as SupabasePatient/SupabaseBooking etc. elsewhere in this project.
[Table("users")]
public class SupabaseUser : BaseModel
{
    [PrimaryKey("id", false)]
    public string Id { get; set; } = string.Empty;

    [Column("full_name")]
    public string? FullName { get; set; }

    [Column("username")]
    public string? Username { get; set; }

    // BCrypt hash only — this table must NEVER get a plaintext password
    // column. See the security note in users_table.sql before syncing
    // this in a real deployment (the anon key that ships inside the app
    // can read/write this table unless RLS is locked down).
    [Column("password_hash")]
    public string? PasswordHash { get; set; }

    [Column("role")]
    public string? Role { get; set; }

    [Column("contact_no")]
    public string? ContactNo { get; set; }

    [Column("email")]
    public string? Email { get; set; }

    [Column("is_active")]
    public bool IsActive { get; set; } = true;

    [Column("is_deleted")]
    public bool IsDeleted { get; set; } = false;

    [Column("failed_login_attempts")]
    public int FailedLoginAttempts { get; set; } = 0;

    [Column("locked_until")]
    public DateTime? LockedUntil { get; set; }

    [Column("last_login_at")]
    public DateTime? LastLoginAt { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [Column("updated_at")]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
