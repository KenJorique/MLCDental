using BC = BCrypt.Net.BCrypt;
using ClinicApp.Models;
using ClinicApp.Models.SupabaseModels;

namespace ClinicApp.Services;

// Thin wrapper around BCrypt so the rest of the app never touches a hashing library directly.
public static class PasswordHasher
{
    private const int WorkFactor = 12;

    // Hashes a plaintext password. Never persist the input string anywhere else.
    public static string Hash(string plaintextPassword)
    {
        if (string.IsNullOrWhiteSpace(plaintextPassword))
            throw new ArgumentException("Password cannot be empty.", nameof(plaintextPassword));

        return BC.HashPassword(plaintextPassword, workFactor: WorkFactor);
    }

    // Verifies a plaintext password against a stored BCrypt hash — never throws, so a bad DB row can't crash login.
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
            // storedHash isn't a valid BCrypt hash (e.g. a leftover plaintext value from before migration).
            return false;
        }
    }

    // Whether a stored hash should be re-hashed at the current work factor.
    public static bool NeedsRehash(string storedHash)
    {
        try { return BC.PasswordNeedsRehash(storedHash, WorkFactor); }
        catch { return true; }
    }
}

public enum LoginFailureReason
{
    None,
    InvalidCredentials,   // wrong identifier, wrong password, or inactive — always shown the same way
    AccountLocked,
}

public class LoginResult
{
    public bool Success { get; init; }
    public User? User { get; init; }
    public LoginFailureReason FailureReason { get; init; } = LoginFailureReason.None;

    // The ONLY string this service ever hands to the UI — never build a more specific message elsewhere.
    public string ErrorMessage { get; init; } = "";

    public static LoginResult Ok(User user) => new() { Success = true, User = user };

    public static LoginResult Fail(string message, LoginFailureReason reason = LoginFailureReason.InvalidCredentials)
        => new() { Success = false, ErrorMessage = message, FailureReason = reason };
}

// Decides whether a person may log in: hashed-password verification, active-account check,
// and brute-force lockout with escalating duration. Deliberately role-agnostic — any active
// staff account can log in regardless of role; what that role can then DO is a separate
// concern handled by SessionService.IsDentist/IsSecretary/etc. elsewhere in the app.
public class AuthenticationService
{
    private readonly DatabaseService _db;

    // Every role allowed to sign in at all — a login-eligibility gate, separate from per-feature
    // access (see SessionService.IsDentist/IsSecretary/IsAdmin/IsAssistant for that). Must match
    // User.Role exactly.
    private static readonly HashSet<string> AllowedRoles = new(StringComparer.OrdinalIgnoreCase)
    {
        "Dentist",
        "Secretary",
        "Admin",
        "Assistant",
    };

    private const int MaxFailedAttempts = 5;
    private const string GenericError = "Invalid email or password.";
    private const string LockedError = "Too many failed attempts. Please try again later.";

    // Injects the local database.
    public AuthenticationService(DatabaseService db)
    {
        _db = db;
    }

    // Validates credentials and account status, returning a generic error regardless of which check failed.
    public async Task<LoginResult> LoginAsync(string identifier, string password)
    {
        // 1) Required-field validation
        if (string.IsNullOrWhiteSpace(identifier) || string.IsNullOrWhiteSpace(password))
            return LoginResult.Fail(GenericError);

        // 2) Retrieve the account. Do NOT reveal whether it exists.
        var user = await _db.GetUserByLoginIdentifierAsync(identifier);
        if (user is null)
        {
            // Constant-ish work so a timing attack can't distinguish "no such user" from "wrong password".
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

        // 6) Role check — correct password but a role not in AllowedRoles is still a deny,
        //    and it must look identical to any other failure to the user.
        if (!AllowedRoles.Contains(user.Role ?? ""))
        {
            LogSecurityEvent("login_denied_wrong_role", user.UserID);
            return LoginResult.Fail(GenericError);
        }

        // 7) Success — role-based feature access is handled elsewhere, via SessionService.
        await _db.RecordSuccessfulLoginAsync(user);
        LogSecurityEvent("login_success", user.UserID);
        return LoginResult.Ok(user);
    }

    // Escalating lockout: 5min, 15min, 30min, 60min, then caps at 2h for further threshold hits.
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

// Auth-related data access, split from the main DatabaseService partial class.
public partial class DatabaseService
{
    // Looks up a user by username or email, case-insensitively. Returns null for no match.
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

    // Records a failed login attempt, locking the account out once the threshold is hit.
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

    // Resets lockout state and records the successful login timestamp.
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

// Holds the current authenticated session — register as a Singleton in MauiProgram.cs.
// Deliberately stores only what the UI needs (id, name, role) — never the password/hash.
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
    public bool IsAdmin => IsAuthenticated && string.Equals(Role, "Admin", StringComparison.OrdinalIgnoreCase);
    public bool IsAssistant => IsAuthenticated && string.Equals(Role, "Assistant", StringComparison.OrdinalIgnoreCase);

    // Raised when the session ends (explicit logout or inactivity timeout) — AppShell should navigate back to login.
    public event Action? SessionEnded;

    // Starts a session for the given user and resets the inactivity clock.
    public void SignIn(User user)
    {
        UserId = user.UserID;
        FullName = user.FullName ?? "";
        Role = user.Role ?? "";
        IsAuthenticated = true;
        ResetInactivityTimer();
    }

    // Call from any user-driven activity (navigation, taps) to keep the session alive.
    public void NotifyActivity()
    {
        if (IsAuthenticated) ResetInactivityTimer();
    }

    // Ends the session and raises SessionEnded if one was active.
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

    // Restarts the inactivity countdown.
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
            // Timer callback runs on a background thread — hop to the UI thread before touching Shell/UI.
            MainThread.BeginInvokeOnMainThread(Logout);
        };
        _inactivityTimer.Start();
    }
}

// Users/staff CRUD against the Supabase "users" table.
public partial class SupabaseDataService
{
    // Fetches every staff account, sorted by name.
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

    // Inserts a new staff account.
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

    // Updates an existing staff account's full row.
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

    // Flips IsDeleted remotely, fetching the full existing row first — updating from a freshly-constructed
    // object with only Id set would send null for every other column, including NOT NULL ones like username.
    public async Task<bool> SoftDeleteUserAsync(string supabaseId)
    {
        try
        {
            await EnsureInitializedAsync();
            if (string.IsNullOrEmpty(supabaseId)) return false;

            var all = await GetUsersAsync();
            var existing = all.FirstOrDefault(u => u.Id == supabaseId);
            if (existing == null)
            {
                System.Diagnostics.Debug.WriteLine($"[Supabase] SoftDeleteUser: no row found for Id={supabaseId}");
                return false;
            }

            existing.IsDeleted = true;
            existing.UpdatedAt = DateTime.UtcNow;
            await _client!.From<SupabaseUser>().Update(existing);
            return true;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[Supabase] SoftDeleteUser FAILED: {ex.Message}");
            return false;
        }
    }
}
