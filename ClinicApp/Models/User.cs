using SQLite;

namespace ClinicApp.Models;

public class User
{
    [PrimaryKey, AutoIncrement]
    public int UserID { get; set; }

    public string? FullName { get; set; }
    public string? Username { get; set; }

    // Legacy plaintext field — kept only so old rows still deserialize.
    public string? Password { get; set; }
    public string? PasswordHash { get; set; }

    public string? Role { get; set; }
    public string? ContactNo { get; set; }
    public string? Email { get; set; }

    public bool IsActive { get; set; } = true;
    public bool IsDeleted { get; set; } = false;

    // ── Brute-force protection ──────────────────────────────
    public int FailedLoginAttempts { get; set; } = 0;
    public DateTime? LockedUntil { get; set; }
    public DateTime? LastLoginAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // Supabase sync
    public string SupabaseId { get; set; } = "";

    // ── NEW: "Remember this device" ─────────────────────────
    // Only a SHA-256 hash of the device token is ever stored here — the
    // raw token itself lives only in that device's SecureStorage (OS
    // keystore/keychain), same principle as PasswordHash never storing
    // the plaintext password. See RememberMeService.cs.
    public string? RememberTokenHash { get; set; }
    public DateTime? RememberTokenExpiresAt { get; set; }

    [Ignore]
    public bool IsLockedOut => LockedUntil.HasValue && LockedUntil.Value > DateTime.UtcNow;
}
