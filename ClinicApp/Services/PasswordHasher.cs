
using BC = BCrypt.Net.BCrypt;
using ClinicApp.Models;
using ClinicApp.Models.SupabaseModels;

namespace ClinicApp.Services;

/// <summary>
/// Thin wrapper around BCrypt so the rest of the app never touches a
/// hashing library directly. Work factor 12 is a reasonable default for
/// a mobile app in 2026 — high enough to be slow for an attacker,
/// low enough not to noticeably delay login on a phone.
/// </summary>
public static class PasswordHasher
{
    private const int WorkFactor = 12;

    /// <summary>Hashes a plaintext password. Never persist the input string anywhere else.</summary>
    public static string Hash(string plaintextPassword)
    {
        if (string.IsNullOrWhiteSpace(plaintextPassword))
            throw new ArgumentException("Password cannot be empty.", nameof(plaintextPassword));

        return BC.HashPassword(plaintextPassword, workFactor: WorkFactor);
    }

    /// <summary>
    /// Verifies a plaintext password against a stored BCrypt hash.
    /// Returns false (never throws) for null/malformed hashes so a bad
    /// DB row can't turn into an unhandled exception during login.
    /// </summary>
    public static bool Verify(string plaintextPassword, string? storedHash)
    {
        if (string.IsNullOrWhiteSpace(plaintextPassword) || string.IsNullOrWhiteSpace(storedHash))
            return false;

        try
        {
            return BC.Verify(plaintextPassword, storedHash);
        }
        catch (BCrypt.Net.SaltParseException)
        {
            // storedHash isn't a valid BCrypt hash (e.g. a leftover plaintext
            // value from before migration) — treat as "does not match".
            return false;
        }
    }

    public static bool NeedsRehash(string storedHash)
    {
        try { return BC.PasswordNeedsRehash(storedHash, WorkFactor); }
        catch { return true; }
    }
}

public enum LoginFailureReason
{
    None,
    InvalidCredentials,   // wrong identifier, wrong password, inactive, or wrong role — always shown the same way
    AccountLocked,
}

public class LoginResult
{
    public bool Success { get; init; }
    public User? User { get; init; }
    public LoginFailureReason FailureReason { get; init; } = LoginFailureReason.None;

    // The ONLY string this service ever hands to the UI. Never build your
    // own message from FailureReason in a way that leaks more detail
    // (e.g. don't say "no such user" vs "wrong password").
    public string ErrorMessage { get; init; } = "";

    public static LoginResult Ok(User user) => new() { Success = true, User = user };

    public static LoginResult Fail(string message, LoginFailureReason reason = LoginFailureReason.InvalidCredentials)
        => new() { Success = false, ErrorMessage = message, FailureReason = reason };
}

/// <summary>
/// The one place that is allowed to decide "yes, this person may use the
/// dentist/secretary side of the app". Every rule from the spec lives
/// here: hashed-password verification, active-account check, exact role
/// check, brute-force lockout with escalating duration, and a single
/// generic error message regardless of which check failed.
/// </summary>
public class AuthenticationService
{
    private readonly DatabaseService _db;

    // Allowed roles for this login. Must match User.Role exactly.
    private static readonly HashSet<string> AllowedRoles = new(StringComparer.OrdinalIgnoreCase)
    {
        "Dentist",
        "Secretary",
    };

    private const int MaxFailedAttempts = 5;
    private const string GenericError = "Invalid email or password.";
    private const string LockedError = "Too many failed attempts. Please try again later.";

    public AuthenticationService(DatabaseService db)
    {
        _db = db;
    }

    public async Task<LoginResult> LoginAsync(string identifier, string password)
    {
        // 1) Required-field validation
        if (string.IsNullOrWhiteSpace(identifier) || string.IsNullOrWhiteSpace(password))
            return LoginResult.Fail(GenericError);

        // 2) Retrieve the account. Do NOT reveal whether it exists.
        var user = await _db.GetUserByLoginIdentifierAsync(identifier);
        if (user is null)
        {
            // Constant-ish work so a timing attack can't distinguish
            // "no such user" from "user exists, wrong password".
            PasswordHasher.Verify(password, PasswordHasher.Hash("decoy-value-not-a-real-password"));
            LogSecurityEvent("login_failed_unknown_identifier");
            return LoginResult.Fail(GenericError);
        }

        // 3) Active check
        if (!user.IsActive)
        {
            LogSecurityEvent("login_failed_inactive", user.UserID);
            return LoginResult.Fail(GenericError);
        }

        // 4) Lockout check (before touching the password hash)
        if (user.IsLockedOut)
        {
            LogSecurityEvent("login_blocked_locked_out", user.UserID);
            return LoginResult.Fail(LockedError, LoginFailureReason.AccountLocked);
        }

        // 5) Password verification (hash only — never plaintext compare)
        bool passwordOk = PasswordHasher.Verify(password, user.PasswordHash);
        if (!passwordOk)
        {
            var lockoutDuration = GetLockoutDuration(user.FailedLoginAttempts + 1);
            await _db.RecordFailedLoginAsync(user, MaxFailedAttempts, lockoutDuration);
            LogSecurityEvent("login_failed_bad_password", user.UserID);
            return LoginResult.Fail(GenericError);
        }

        // 6) Role check — correct password but wrong role is still a deny,
        //    and it must look identical to any other failure to the user.
        if (!AllowedRoles.Contains(user.Role ?? ""))
        {
            LogSecurityEvent("login_denied_wrong_role", user.UserID);
            return LoginResult.Fail(GenericError);
        }

        // 7) Success
        await _db.RecordSuccessfulLoginAsync(user);
        LogSecurityEvent("login_success", user.UserID);
        return LoginResult.Ok(user);
    }

    // Escalating lockout: 5min, 15min, 30min, 60min, then caps at 2h for
    // any further threshold hits, so a persistent attacker doesn't get a
    // fixed, predictable retry window.
    private static TimeSpan GetLockoutDuration(int failedAttempts)
    {
        if (failedAttempts < MaxFailedAttempts) return TimeSpan.Zero; // not locked yet
        int strikesOverThreshold = failedAttempts - MaxFailedAttempts; // 0, 1, 2, 3...
        return strikesOverThreshold switch
        {
            0 => TimeSpan.FromMinutes(5),
            1 => TimeSpan.FromMinutes(15),
            2 => TimeSpan.FromMinutes(30),
            3 => TimeSpan.FromMinutes(60),
            _ => TimeSpan.FromHours(2),
        };
    }

    // Security-event logging only — never pass password or hash values in here.
    private static void LogSecurityEvent(string eventName, int? userId = null)
    {
        System.Diagnostics.Debug.WriteLine(
            $"[Auth] {eventName} userId={(userId.HasValue ? userId.Value.ToString() : "n/a")} at {DateTime.UtcNow:O}");
    }
}

/// <summary>
/// Auth-related data access, split out from the main DatabaseService
/// partial class so the (already huge) DatabaseService.cs doesn't grow
/// further. Everything here talks to the same local SQLite connection
/// via the shared Init()/_database from DatabaseService.cs.
/// </summary>
public partial class DatabaseService
{
    /// <summary>
    /// Looks a user up by username OR email, case-insensitively.
    /// Returns null for no match — callers must not distinguish this
    /// from "wrong password" in anything shown to the user.
    /// </summary>
    public async Task<User?> GetUserByLoginIdentifierAsync(string identifier)
    {
        await Init();
        if (string.IsNullOrWhiteSpace(identifier)) return null;

        var needle = identifier.Trim().ToLowerInvariant();

        var all = await _database!.Table<User>()
            .Where(u => !u.IsDeleted)
            .ToListAsync();

        return all.FirstOrDefault(u =>
            (u.Username != null && u.Username.ToLowerInvariant() == needle) ||
            (u.Email != null && u.Email.ToLowerInvariant() == needle));
    }

    public async Task RecordFailedLoginAsync(User user, int maxAttempts, TimeSpan lockoutDuration)
    {
        await Init();
        user.FailedLoginAttempts += 1;

        if (user.FailedLoginAttempts >= maxAttempts)
        {
            user.LockedUntil = DateTime.UtcNow.Add(lockoutDuration);
        }

        user.UpdatedAt = DateTime.UtcNow;
        await _database!.UpdateAsync(user);
    }

    public async Task RecordSuccessfulLoginAsync(User user)
    {
        await Init();
        user.FailedLoginAttempts = 0;
        user.LockedUntil = null;
        user.LastLoginAt = DateTime.UtcNow;
        user.UpdatedAt = DateTime.UtcNow;
        await _database!.UpdateAsync(user);
    }
}


/// <summary>
/// Holds the current authenticated session. Register this as a Singleton
/// in MauiProgram.cs — there is exactly one of these for the app's
/// lifetime, and it is the single source of truth for "who is logged in
/// right now" that every page/service checks before doing anything
/// sensitive.
///
/// Deliberately stores only what the UI needs (id, name, role) — never
/// the password or its hash — so nothing sensitive sits in memory longer
/// than it has to.
/// </summary>
public class SessionService
{
    // Auto logout after this much inactivity.
    private static readonly TimeSpan InactivityTimeout = TimeSpan.FromMinutes(55);

    private System.Timers.Timer? _inactivityTimer;

    public int UserId { get; private set; }
    public string FullName { get; private set; } = "";
    public string Role { get; private set; } = "";
    public bool IsAuthenticated { get; private set; }

    public bool IsDentist => IsAuthenticated && string.Equals(Role, "Dentist", StringComparison.OrdinalIgnoreCase);
    public bool IsSecretary => IsAuthenticated && string.Equals(Role, "Secretary", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Raised when the session ends — either an explicit Logout() call or
    /// an inactivity timeout. Subscribe to this in AppShell to force
    /// navigation back to the login page and clear the nav stack.
    /// </summary>
    public event Action? SessionEnded;

    public void SignIn(User user)
    {
        UserId = user.UserID;
        FullName = user.FullName ?? "";
        Role = user.Role ?? "";
        IsAuthenticated = true;
        ResetInactivityTimer();
    }

    /// <summary>Call this from any user-driven activity (page navigation, button taps) to keep the session alive.</summary>
    public void NotifyActivity()
    {
        if (IsAuthenticated) ResetInactivityTimer();
    }

    public void Logout()
    {
        bool wasAuthenticated = IsAuthenticated;

        UserId = 0;
        FullName = "";
        Role = "";
        IsAuthenticated = false;

        _inactivityTimer?.Stop();
        _inactivityTimer?.Dispose();
        _inactivityTimer = null;

        if (wasAuthenticated)
            SessionEnded?.Invoke();
    }

    private void ResetInactivityTimer()
    {
        _inactivityTimer?.Stop();
        _inactivityTimer?.Dispose();

        _inactivityTimer = new System.Timers.Timer(InactivityTimeout.TotalMilliseconds)
        {
            AutoReset = false,
        };
        _inactivityTimer.Elapsed += (_, _) =>
        {
            // Timer callback runs on a background thread — hop to the UI
            // thread before touching anything Shell/UI related downstream.
            MainThread.BeginInvokeOnMainThread(Logout);
        };
        _inactivityTimer.Start();
    }
}

/// <summary>
/// Auth-related data access, split out from the main DatabaseService
/// partial class so the (already huge) DatabaseService.cs doesn't grow
/// further. Everything here talks to the same local SQLite connection
/// via the shared Init()/_database from DatabaseService.cs.
/// </summary>
/// <summary>
/// Local-SQLite side of User ↔ Supabase syncing — the User-table analogue
/// of the Patient SupabaseId linking already in DatabaseService.cs
/// (GetPatientBySupabaseId / BackfillSupabaseIds). Requires the
/// "SupabaseId" column added to the User table (see Init() note below)
/// and the SupabaseId property already on Models/User.cs.
/// </summary>
public partial class DatabaseService
{
    // Add this one line inside Init(), next to the other
    // "ALTER TABLE User ADD COLUMN ..." lines:
    //
    //   try { await _database.ExecuteAsync("ALTER TABLE User ADD COLUMN SupabaseId TEXT DEFAULT ''"); }
    //   catch { /* already exists */ }

    public async Task<User?> GetUserBySupabaseId(string supabaseId)
    {
        await Init();
        return await _database!.Table<User>()
            .Where(u => u.SupabaseId == supabaseId)
            .FirstOrDefaultAsync();
    }

    // Matches by Username, since — unlike patients — it's guaranteed
    // unique and stable, rather than a fuzzy name+phone heuristic.
    public async Task BackfillUserSupabaseIds(List<SupabaseUser> supabaseUsers)
    {
        await Init();
        foreach (var su in supabaseUsers)
        {
            if (string.IsNullOrEmpty(su.Id) || string.IsNullOrEmpty(su.Username)) continue;

            var local = await _database!.Table<User>()
                .Where(u => u.Username == su.Username && u.SupabaseId == "")
                .FirstOrDefaultAsync();

            if (local != null)
            {
                local.SupabaseId = su.Id;
                await _database!.UpdateAsync(local);
                System.Diagnostics.Debug.WriteLine(
                    $"[Backfill] Linked UserID={local.UserID} → SupabaseId={su.Id}");
            }
        }
    }

    // Called right after a successful SupabaseDataService.AddUserAsync so
    // the local row remembers the remote row's id for future updates/deletes.
    public async Task SetUserSupabaseId(int userId, string supabaseId)
    {
        await Init();
        var user = await _database!.Table<User>().Where(u => u.UserID == userId).FirstOrDefaultAsync();
        if (user is null) return;
        user.SupabaseId = supabaseId;
        await _database!.UpdateAsync(user);
    }
}


/// <summary>
/// Users/staff CRUD against the Supabase "users" table. Split into its own
/// partial-class file for the same reason as the rest of this service is
/// getting split up — SupabaseDataService.cs is already huge.
/// </summary>
public partial class SupabaseDataService
{
    public async Task<List<SupabaseUser>> GetUsersAsync()
    {
        try
        {
            await EnsureInitializedAsync();
            var result = await _client!
                .From<SupabaseUser>()
                .Order("full_name", Supabase.Postgrest.Constants.Ordering.Ascending)
                .Get();
            return result.Models ?? new List<SupabaseUser>();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[Supabase] GetUsers: {ex.Message}");
            return new List<SupabaseUser>();
        }
    }

    public async Task<SupabaseUser?> AddUserAsync(SupabaseUser user)
    {
        try
        {
            await EnsureInitializedAsync();
            var result = await _client!.From<SupabaseUser>().Insert(user);
            var saved = result.Models.FirstOrDefault();
            System.Diagnostics.Debug.WriteLine(
                $"[Supabase] INSERT user result Id={saved?.Id ?? "NULL — check RLS policies"}");
            return saved;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[Supabase] AddUser FAILED: {ex.Message}");
            return null;
        }
    }

    public async Task<bool> UpdateUserAsync(SupabaseUser user)
    {
        try
        {
            await EnsureInitializedAsync();

            if (string.IsNullOrEmpty(user.Id))
            {
                System.Diagnostics.Debug.WriteLine("[Supabase] UpdateUser: Id is empty — cannot update");
                return false;
            }

            var result = await _client!.From<SupabaseUser>().Update(user);
            System.Diagnostics.Debug.WriteLine($"[Supabase] UpdateUser done. Rows: {result.Models.Count}");
            return true;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[Supabase] UpdateUser FAILED: {ex.Message}");
            return false;
        }
    }

    // Soft delete to match local IsDeleted semantics — flips the flag
    // remotely rather than removing the row, so other devices that pull
    // this user still see why they disappeared instead of a hard 404.
    public async Task<bool> SoftDeleteUserAsync(SupabaseUser user)
    {
        try
        {
            await EnsureInitializedAsync();
            if (string.IsNullOrEmpty(user.Id)) return false;

            user.IsDeleted = true;
            user.UpdatedAt = DateTime.UtcNow;
            await _client!.From<SupabaseUser>().Update(user);
            return true;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[Supabase] SoftDeleteUser FAILED: {ex.Message}");
            return false;
        }
    }
}
