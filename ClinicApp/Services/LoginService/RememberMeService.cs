using Plugin.Maui.Biometric;
using System.Security.Cryptography;
using System.Text;
using ClinicApp.Models;
namespace ClinicApp.Services;

/// <summary>
/// "Remember this device" for 30 days, optionally gated behind biometric
/// confirmation. Design:
///
///   - A random 256-bit token is generated and stored RAW only in
///     SecureStorage (OS-level Keychain/Keystore) — never in SQLite.
///     Only SHA-256(token) is stored in the User row.
///   - If the device has biometrics enrolled AND the person opted in at
///     login, a plain (non-secret) flag is also saved noting that this
///     token requires a successful fingerprint/Face ID/Windows Hello
///     prompt before it's used to sign in.
///   - Biometric does NOT replace the token or prove identity by itself —
///     it only gates USE of the token. Someone without any biometric
///     enrolled, or whose device lacks the hardware, simply never sees
///     the option and gets the exact same silent auto-login as before.
///   - Checked ONLY on cold start (App.xaml.cs), never mid-session — see
///     the original note on TryAutoLoginAsync for why.
/// </summary>
public class RememberMeService
{
    private const string TokenStorageKey = "clinicapp_remember_token";
    private const string BiometricGatePrefKey = "clinicapp_remember_requires_biometric";
    private static readonly TimeSpan TokenLifetime = TimeSpan.FromDays(30);

    private static readonly HashSet<string> AllowedRoles = new(StringComparer.OrdinalIgnoreCase)
    {
        "Dentist",
        "Secretary",
    };

    private readonly DatabaseService _db;
    private readonly BiometricService _biometric; // ── NEW ──

    public RememberMeService(DatabaseService db, BiometricService biometric) // ── CHANGED ──
    {
        _db = db;
        _biometric = biometric;
    }

    /// <summary>
    /// Whether this device can even offer the biometric option — used by
    /// LoginPage to decide whether to show the extra toggle at all.
    /// </summary>
    public Task<bool> IsBiometricAvailableAsync() => _biometric.IsAvailableAsync();
    public Task<bool> ConfirmBiometricAsync(string reason) => _biometric.AuthenticateAsync(reason);

    public async Task RememberAsync(int userId, bool requireBiometric)
    {
        var tokenBytes = RandomNumberGenerator.GetBytes(32);
        var token = Convert.ToBase64String(tokenBytes);
        var hash = Hash(token);
        var expiresAt = DateTime.UtcNow.Add(TokenLifetime);

        await _db.SetRememberTokenAsync(userId, hash, expiresAt);
        await SecureStorage.Default.SetAsync(TokenStorageKey, token);

        // Plain UI preference, not a secret — fine in Preferences rather
        // than SecureStorage. It only ever gates a token that's already
        // itself securely stored; losing this flag on its own can't leak
        // anything.
        Preferences.Default.Set(BiometricGatePrefKey, requireBiometric);
    }

    /// <summary>
    /// Call once, on cold start. Returns the user to auto-sign-in as, or
    /// null if there's no token, it's invalid/expired, the account no
    /// longer qualifies, OR (when biometric was required) the biometric
    /// prompt failed/was cancelled/is no longer available. In every "null"
    /// case the caller's existing fallback — showing the plain login page
    /// — is exactly the right behavior, so nothing else needs to change.
    /// </summary>
    public async Task<User?> TryAutoLoginAsync()
    {
        string? token;
        try
        {
            token = await SecureStorage.Default.GetAsync(TokenStorageKey);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[RememberMe] SecureStorage read failed: {ex.Message}");
            return null;
        }

        if (string.IsNullOrEmpty(token)) return null;

        var hash = Hash(token);
        var user = await _db.GetUserByValidRememberTokenHashAsync(hash);

        if (user is null || !AllowedRoles.Contains(user.Role ?? ""))
        {
            await ForgetDeviceOnlyAsync();
            return null;
        }

        // ── NEW: biometric gate ──
        bool requiresBiometric = Preferences.Default.Get(BiometricGatePrefKey, false);
        if (requiresBiometric)
        {
            bool stillAvailable = await _biometric.IsAvailableAsync();
            if (!stillAvailable)
            {
                // Enrollment was removed/hardware changed since this was
                // set up. Deliberately do NOT silently fall back to
                // skipping the gate — that would be a quiet security
                // downgrade. Just require a normal password login instead;
                // the token itself is left intact in case biometrics come
                // back (e.g. a temporarily-locked sensor).
                System.Diagnostics.Debug.WriteLine("[RememberMe] Biometric gate set but no longer available — requiring password login.");
                return null;
            }

            bool confirmed = await _biometric.AuthenticateAsync(
                $"Unlock MLC Dental as {user.FullName}");

            if (!confirmed)
            {
                // Cancelled or failed the prompt — not an error, just
                // fall through to the password screen. Token stays valid
                // for next time.
                return null;
            }
        }

        // Sliding window: extend another 30 days from this successful use.
        await _db.RefreshRememberTokenAsync(user.UserID, DateTime.UtcNow.Add(TokenLifetime));
        return user;
    }

    public async Task ForgetAsync(int userId)
    {
        await _db.ClearRememberTokenAsync(userId);
        SecureStorage.Default.Remove(TokenStorageKey);
        Preferences.Default.Remove(BiometricGatePrefKey); // ── NEW ──
    }

    private Task ForgetDeviceOnlyAsync()
    {
        SecureStorage.Default.Remove(TokenStorageKey);
        Preferences.Default.Remove(BiometricGatePrefKey); // ── NEW ──
        return Task.CompletedTask;
    }

    private static string Hash(string token)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(token));
        return Convert.ToHexString(bytes);
    }
}


/// <summary>
/// Thin wrapper around Plugin.Maui.Biometric so the rest of the app never
/// touches the plugin directly. Biometric is only a GATE in front of
/// RememberMeService's existing token — it never stores or proves identity
/// by itself.
/// </summary>
public class BiometricService
{
    public async Task<bool> IsAvailableAsync()
    {
        try
        {
#if ANDROID
            // The plugin's own status check reports Failure even when Android
            // says biometrics are usable, so ask the OS directly.
            var mgr = AndroidX.Biometric.BiometricManager.From(Android.App.Application.Context);
            int strong = mgr.CanAuthenticate(AndroidX.Biometric.BiometricManager.Authenticators.BiometricStrong);
            System.Diagnostics.Debug.WriteLine($"[Biometric] Android CanAuthenticate strong={strong}");
            return strong == 0; // 0 = BIOMETRIC_SUCCESS
#else
            var status = await BiometricAuthenticationService.Default.GetAuthenticationStatusAsync();
            System.Diagnostics.Debug.WriteLine($"[Biometric] Availability status = {status}");
            return status == BiometricHwStatus.Success;
#endif
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[Biometric] IsAvailable check failed: {ex}");
            return false;
        }
    }

    public async Task<bool> AuthenticateAsync(string reason)
    {
        try
        {
            var request = new AuthenticationRequest
            {
                Title = "MLC Dental",
                Subtitle = reason,
                NegativeText = "Use password instead",
                AllowPasswordAuth = false,
            };

            var result = await BiometricAuthenticationService.Default
                .AuthenticateAsync(request, CancellationToken.None);

            System.Diagnostics.Debug.WriteLine($"[Biometric] Authenticate status = {result.Status}");
            return result.Status == BiometricResponseStatus.Success;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[Biometric] Authenticate failed: {ex}");
            return false;
        }
    }
}