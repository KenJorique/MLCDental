using System.Security.Cryptography;
using System.Text;
using ClinicApp.Models;

namespace ClinicApp.Services;

/// <summary>
/// "Remember this device" for 30 days. Design:
///
///   - A random 256-bit token is generated and stored RAW only in
///     SecureStorage (OS-level Keychain on iOS/macOS, Keystore-backed
///     encrypted storage on Android/Windows) — never in the SQLite DB.
///   - Only SHA-256(token) is stored in the User row, via
///     DatabaseService.RememberMe.cs — exactly the same "never store the
///     real secret" principle as PasswordHash.
///   - On cold start, App.xaml.cs asks TryAutoLoginAsync() to check the
///     token; if it matches an active, still-allowed-role account and
///     hasn't expired, that user is silently signed in.
///   - Deliberately checked ONLY on cold start (App constructor), not
///     whenever the login screen reappears mid-session — otherwise it
///     would silently undo SessionService's 15-minute inactivity
///     timeout the moment it fires. An explicit Logout() clears the
///     token (see ClearAsync); an inactivity timeout does not, since the
///     person didn't choose to sign out, they just stepped away while
///     the app was still running.
///
/// Trade-off worth knowing: this token is as trustworthy as the device's
/// OS-level secure storage. Anyone who can extract it from a compromised/
/// jailbroken device gets a 30-day login as that staff member. That's the
/// standard "remember me" trade-off every app with this feature accepts —
/// it's why it's opt-in via a checkbox on the login screen, not the
/// default.
/// </summary>
public class RememberMeService
{
    private const string StorageKey = "clinicapp_remember_token";
    private static readonly TimeSpan TokenLifetime = TimeSpan.FromDays(30);

    // Kept in sync with AuthenticationService's AllowedRoles by hand —
    // if you ever add a role there, add it here too. Small duplication,
    // but keeps RememberMeService from needing to know about
    // AuthenticationService's internals.
    private static readonly HashSet<string> AllowedRoles = new(StringComparer.OrdinalIgnoreCase)
    {
        "Dentist",
        "Secretary",
    };

    private readonly DatabaseService _db;

    public RememberMeService(DatabaseService db)
    {
        _db = db;
    }

    public async Task RememberAsync(int userId)
    {
        var tokenBytes = RandomNumberGenerator.GetBytes(32); // 256 bits
        var token = Convert.ToBase64String(tokenBytes);
        var hash = Hash(token);
        var expiresAt = DateTime.UtcNow.Add(TokenLifetime);

        await _db.SetRememberTokenAsync(userId, hash, expiresAt);
        await SecureStorage.Default.SetAsync(StorageKey, token);
    }

    /// <summary>
    /// Call once, on cold start. Returns the user to auto-sign-in as, or
    /// null if there's no token, it's invalid/expired, or the account no
    /// longer qualifies (inactive, deleted, or role changed away from
    /// Dentist/Secretary). Automatically clears a stale/invalid token so
    /// it doesn't keep getting checked on every future launch.
    /// </summary>
    public async Task<User?> TryAutoLoginAsync()
    {
        string? token;
        try
        {
            token = await SecureStorage.Default.GetAsync(StorageKey);
        }
        catch (Exception ex)
        {
            // SecureStorage can throw on some platforms/OS states (e.g.
            // keystore reset after a factory-reset-like event) — treat
            // as "no token", don't crash startup over it.
            System.Diagnostics.Debug.WriteLine($"[RememberMe] SecureStorage read failed: {ex.Message}");
            return null;
        }

        if (string.IsNullOrEmpty(token)) return null;

        var hash = Hash(token);
        var user = await _db.GetUserByValidRememberTokenHashAsync(hash);

        if (user is null || !AllowedRoles.Contains(user.Role ?? ""))
        {
            await ForgetDeviceOnlyAsync(); // clear the now-useless local token
            return null;
        }

        // Sliding window: extend another 30 days from this successful use.
        await _db.RefreshRememberTokenAsync(user.UserID, DateTime.UtcNow.Add(TokenLifetime));
        return user;
    }

    // Explicit logout — revokes the DB side AND clears the device token,
    // so this device genuinely requires a fresh login next time.
    public async Task ForgetAsync(int userId)
    {
        await _db.ClearRememberTokenAsync(userId);
        SecureStorage.Default.Remove(StorageKey);
    }

    // Clears only the local SecureStorage token, without touching the DB
    // (used when the stored token turned out to be invalid/expired
    // anyway, so there's nothing meaningful left to revoke server-side).
    private Task ForgetDeviceOnlyAsync()
    {
        SecureStorage.Default.Remove(StorageKey);
        return Task.CompletedTask;
    }

    private static string Hash(string token)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(token));
        return Convert.ToHexString(bytes); // uppercase hex — fine, just needs to be consistent
    }
}

/// <summary>
/// Local-DB half of "Remember this device" — storing/checking/clearing
/// the token hash. Paired with RememberMeService.cs, which owns the
/// actual token generation/hashing and SecureStorage access.
///
/// Requires two new columns on User (see the ALTER TABLE lines noted
/// below — add them inside DatabaseService.Init(), next to the other
/// "ALTER TABLE User ADD COLUMN ..." lines):
///
///   try { await _database.ExecuteAsync("ALTER TABLE User ADD COLUMN RememberTokenHash TEXT"); }
///   catch { /* already exists */ }
///   try { await _database.ExecuteAsync("ALTER TABLE User ADD COLUMN RememberTokenExpiresAt TEXT"); }
///   catch { /* already exists */ }
/// </summary>
public partial class DatabaseService
{
    public async Task SetRememberTokenAsync(int userId, string tokenHash, DateTime expiresAtUtc)
    {
        await Init();
        var user = await _database!.Table<User>().Where(u => u.UserID == userId).FirstOrDefaultAsync();
        if (user is null) return;

        user.RememberTokenHash = tokenHash;
        user.RememberTokenExpiresAt = expiresAtUtc;
        user.UpdatedAt = DateTime.UtcNow;
        await _database!.UpdateAsync(user);
    }

    /// <summary>
    /// Returns the user this hash belongs to, but only if the token
    /// hasn't expired, the account is active, and it isn't soft-deleted.
    /// Does NOT check role here — RememberMeService re-validates against
    /// the same allowed-roles list AuthenticationService uses, so the two
    /// stay in sync in one place.
    /// </summary>
    public async Task<User?> GetUserByValidRememberTokenHashAsync(string tokenHash)
    {
        await Init();
        var user = await _database!.Table<User>()
            .Where(u => u.RememberTokenHash == tokenHash && !u.IsDeleted)
            .FirstOrDefaultAsync();

        if (user is null) return null;
        if (!user.IsActive) return null;
        if (!user.RememberTokenExpiresAt.HasValue || user.RememberTokenExpiresAt.Value <= DateTime.UtcNow)
            return null;

        return user;
    }

    // Refreshes the expiry (sliding 30-day window) after a successful
    // auto-login, so a device stays "remembered" as long as it's opened
    // at least once every 30 days.
    public async Task RefreshRememberTokenAsync(int userId, DateTime newExpiresAtUtc)
    {
        await Init();
        var user = await _database!.Table<User>().Where(u => u.UserID == userId).FirstOrDefaultAsync();
        if (user is null) return;
        user.RememberTokenExpiresAt = newExpiresAtUtc;
        await _database!.UpdateAsync(user);
    }

    // Called on explicit Logout — revokes this device's ability to
    // auto-login, unlike an inactivity timeout which leaves it intact
    // (see RememberMeService.cs for why that distinction matters).
    public async Task ClearRememberTokenAsync(int userId)
    {
        await Init();
        var user = await _database!.Table<User>().Where(u => u.UserID == userId).FirstOrDefaultAsync();
        if (user is null) return;
        user.RememberTokenHash = null;
        user.RememberTokenExpiresAt = null;
        await _database!.UpdateAsync(user);
    }
}
