using ClinicApp.Models;
using Supabase;

namespace ClinicApp.Services
{
    public class SupabaseDataService
    {
        private Client? _client;
        private readonly string _url;
        private readonly string _key;
        private bool _initialized = false;
        private readonly SemaphoreSlim _initLock = new(1, 1);

        public Client Client => _client!;

        public SupabaseDataService(string url, string key)
        {
            _url = url;
            _key = key;
        }

        public async Task EnsureInitializedAsync()
        {
            if (_initialized) return;
            await _initLock.WaitAsync();
            try
            {
                if (_initialized) return;
                var options = new SupabaseOptions { AutoConnectRealtime = false };
                _client = new Client(_url, _key, options);
                await _client.InitializeAsync();
                _initialized = true;
            }
            finally
            {
                _initLock.Release();
            }
        }

        // ── Patients ──────────────────────────────────
        public async Task<List<SupabasePatient>> GetPatientsAsync()
        {
            try
            {
                await EnsureInitializedAsync();
                var result = await _client!
                    .From<SupabasePatient>()
                    .Order("date_registered",
                           Supabase.Postgrest.Constants.Ordering.Descending)
                    .Get();
                return result.Models ?? new List<SupabasePatient>();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[Supabase] GetPatients: {ex.Message}");
                return new List<SupabasePatient>();
            }
        }

        public async Task<SupabasePatient?> AddPatientAsync(SupabasePatient patient)
        {
            await EnsureInitializedAsync();

            System.Diagnostics.Debug.WriteLine(
                $"[Supabase] INSERT patients: {patient.FirstName} {patient.LastName}");

            var result = await _client!.From<SupabasePatient>().Insert(patient);
            var saved = result.Models.FirstOrDefault();

            System.Diagnostics.Debug.WriteLine(
                $"[Supabase] INSERT result Id={saved?.Id ?? "NULL — check RLS policies"}");

            return saved;
        }

        public async Task<bool> UpdatePatientAsync(SupabasePatient patient)
        {
            try
            {
                await EnsureInitializedAsync();

                if (string.IsNullOrEmpty(patient.Id))
                {
                    System.Diagnostics.Debug.WriteLine("[Supabase] UpdatePatient: Id is empty — cannot update");
                    return false;
                }

                System.Diagnostics.Debug.WriteLine($"[Supabase] Updating patient Id={patient.Id}");

                // Direct update using the model — supabase-csharp matches by PrimaryKey
                var result = await _client!.From<SupabasePatient>().Update(patient);
                System.Diagnostics.Debug.WriteLine($"[Supabase] Update done. Rows: {result.Models.Count}");
                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[Supabase] UpdatePatient FAILED: {ex.Message}");
                return false;
            }
        }

        public async Task DeletePatientAsync(SupabasePatient patient)
        {
            try
            {
                await EnsureInitializedAsync();
                await _client!.From<SupabasePatient>().Delete(patient);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[Supabase] DeletePatient: {ex.Message}");
            }
        }

        // ── Bookings ──────────────────────────────────
        public async Task<List<SupabaseBooking>> GetPendingBookingsAsync()
        {
            try
            {
                await EnsureInitializedAsync();
                var result = await _client!
                    .From<SupabaseBooking>()
                    .Where(b => b.Status == "pending")
                    .Get();
                return result.Models ?? new List<SupabaseBooking>();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[Supabase] GetPendingBookings: {ex.Message}");
                return new List<SupabaseBooking>();
            }
        }

        // Fixed — correct supabase-csharp update API
        public async Task UpdateBookingStatusAsync(string bookingId, string status)
        {
            try
            {
                await EnsureInitializedAsync();

                // Fetch the full row first
                var response = await _client!
                    .From<SupabaseBooking>()
                    .Where(b => b.Id == bookingId)
                    .Single();

                if (response == null)
                {
                    System.Diagnostics.Debug.WriteLine($"[UpdateBooking] Booking {bookingId} not found");
                    return;
                }

                // Mutate and update the hydrated model
                response.Status = status;
                await _client!.From<SupabaseBooking>().Update(response);

                System.Diagnostics.Debug.WriteLine($"[UpdateBooking] {bookingId} → {status}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[Supabase] UpdateBookingStatus: {ex.Message}");
                throw; // rethrow so ViewModel catches it and shows error
            }
        }
        public async Task<List<SupabaseBooking>> GetBookingsByStatusAsync(string status)
        {
            try
            {
                await EnsureInitializedAsync();
                var result = await _client!
                    .From<SupabaseBooking>()
                    .Where(b => b.Status == status)
                    .Order("created_at", Supabase.Postgrest.Constants.Ordering.Descending)
                    .Get();
                return result.Models ?? new List<SupabaseBooking>();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[Supabase] GetBookingsByStatus: {ex.Message}");
                return new List<SupabaseBooking>();
            }
        }

        // ── Appointment Entries ───────────────────────────────────────

        public async Task<SupabaseAppointmentEntry?> AddAppointmentEntryAsync(
            SupabaseAppointmentEntry entry)
        {
            try
            {
                await EnsureInitializedAsync();
                var result = await _client!
                    .From<SupabaseAppointmentEntry>()
                    .Insert(entry);
                return result.Models.FirstOrDefault();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(
                    $"[Supabase] AddAppointmentEntry: {ex.Message}");
                return null;
            }
        }

        public async Task<List<SupabaseAppointmentEntry>> GetAppointmentEntriesAsync()
        {
            try
            {
                await EnsureInitializedAsync();
                var result = await _client!
                    .From<SupabaseAppointmentEntry>()
                    .Order("appointment_datetime",
                           Supabase.Postgrest.Constants.Ordering.Ascending)
                    .Get();
                return result.Models ?? new List<SupabaseAppointmentEntry>();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(
                    $"[Supabase] GetAppointmentEntries: {ex.Message}");
                return new List<SupabaseAppointmentEntry>();
            }
        }

        public async Task UpdateAppointmentEntryStatusAsync(string supabaseId, string status)
        {
            try
            {
                await EnsureInitializedAsync();
                var result = await _client!
                    .From<SupabaseAppointmentEntry>()
                    .Where(a => a.Id == supabaseId)
                    .Single();
                if (result == null) return;
                result.Status = status;
                await _client!.From<SupabaseAppointmentEntry>().Update(result);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(
                    $"[Supabase] UpdateAppointmentEntryStatus: {ex.Message}");
            }
        }

        public async Task DeleteBookingAsync(string bookingId)
        {
            try
            {
                await EnsureInitializedAsync();
                var result = await _client!
                    .From<SupabaseBooking>()
                    .Where(b => b.Id == bookingId)
                    .Single();
                if (result == null) return;
                await _client!.From<SupabaseBooking>().Delete(result);
                System.Diagnostics.Debug.WriteLine(
                    $"[Supabase] Booking {bookingId} deleted.");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(
                    $"[Supabase] DeleteBooking: {ex.Message}");
            }
        }

        public async Task DeleteAppointmentEntryAsync(string supabaseId)
        {
            try
            {
                await EnsureInitializedAsync();
                var result = await _client!
                    .From<SupabaseAppointmentEntry>()
                    .Where(a => a.Id == supabaseId)
                    .Single();
                if (result == null) return;
                await _client!.From<SupabaseAppointmentEntry>().Delete(result);
                System.Diagnostics.Debug.WriteLine(
                    $"[Supabase] AppointmentEntry {supabaseId} deleted.");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(
                    $"[Supabase] DeleteAppointmentEntry: {ex.Message}");
            }
        }

        // Temporary debug method — gets ALL bookings regardless of status
        public async Task<List<SupabaseBooking>> GetAllBookingsDebugAsync()
        {
            try
            {
                await EnsureInitializedAsync();
                var result = await _client!.From<SupabaseBooking>().Get();
                return result.Models ?? new List<SupabaseBooking>();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(
                    $"[Supabase] GetAllBookingsDebug: {ex.Message}");
                return new List<SupabaseBooking>();
            }
        }

        public async Task<List<SupabaseBooking>> GetBookingsForWeekAsync(
    DateTime weekStart, DateTime weekEnd)
        {
            try
            {
                await EnsureInitializedAsync();
                var result = await _client!
                    .From<SupabaseBooking>()
                    .Get();

                return result.Models
                    .Where(b =>
                    {
                        var inRange = b.AppointmentDate >= weekStart
                                   && b.AppointmentDate < weekEnd;
                        var notDone = b.Status != "completed"
                                   && b.Status != "rejected";
                        return inRange && notDone;
                    })
                    .OrderBy(b => b.AppointmentDate)
                    .ToList();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(
                    $"[Supabase] GetBookingsForWeek: {ex.Message}");
                return new List<SupabaseBooking>();
            }
        }

        public async Task<string?> SyncToGoogleTasksAsync(
             string accessToken,
             string patientName,
             string service,
             DateTime appointmentDateTime,
             string phone,
             string notes = "")
        {
            try
            {
                // Format date for display
                var localTime = appointmentDateTime.Kind == DateTimeKind.Utc
                    ? appointmentDateTime.ToLocalTime()
                    : appointmentDateTime;

                var formattedDate = localTime.ToString("MMMM dd, yyyy h:mm tt");

                var task = new
                {
                    title = $"Appointment: {patientName} — {service}",
                    notes = $"Patient: {patientName}\n" +
                             $"Service: {service}\n" +
                             $"Date: {formattedDate}\n" +
                             $"Phone: {phone}" +
                             (string.IsNullOrEmpty(notes) ? "" : $"\nNotes: {notes}"),
                    due = localTime.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ss.fffZ"),
                    status = "needsAction"
                };

                var json = System.Text.Json.JsonSerializer.Serialize(task);
                var content = new StringContent(
                    json, System.Text.Encoding.UTF8, "application/json");

                // Call Google Tasks API directly — no Edge Function needed
                using var http = new HttpClient();
                http.DefaultRequestHeaders.Add(
                    "Authorization", $"Bearer {accessToken}");

                var response = await http.PostAsync(
                    "https://tasks.googleapis.com/tasks/v1/lists/@default/tasks",
                    content);

                var responseText = await response.Content.ReadAsStringAsync();
                System.Diagnostics.Debug.WriteLine(
                    $"[GoogleTasks] Response: {responseText}");

                if (response.IsSuccessStatusCode)
                {
                    var doc = System.Text.Json.JsonDocument.Parse(responseText);
                    return doc.RootElement
                              .TryGetProperty("id", out var id)
                              ? id.GetString() : null;
                }

                System.Diagnostics.Debug.WriteLine(
                    $"[GoogleTasks] Failed: {response.StatusCode} — {responseText}");
                return null;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(
                    $"[GoogleTasks] Exception: {ex.Message}");
                return null;
            }
        }

        public async System.Threading.Tasks.Task CompleteGoogleTaskAsync(
    string accessToken, string taskId)
        {
            try
            {
                var payload = new { accessToken, taskId, action = "complete" };
                var json = System.Text.Json.JsonSerializer.Serialize(payload);
                using var http = new HttpClient();
                http.DefaultRequestHeaders.Add("Authorization", $"Bearer {_key}");
                await http.PostAsync(
                    $"{_url}/functions/v1/sync-to-calendar",
                    new StringContent(json,
                        System.Text.Encoding.UTF8, "application/json"));
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(
                    $"[CompleteTask] {ex.Message}");
            }
        }

        public async Task<string?> GetFreshAccessTokenAsync()
        {
            try
            {
                var refreshToken = Preferences.Get("google_refresh_token",
                    "1//04tLkx0PporPuCgYIARAAGAQSNwF-L9IrtBP1_vCvTHOIQfIvnVavedyj6G0ErX6jjRRLnO4Ab0oa9H_3lDrLfiRdXale-LZdWzM");

                if (string.IsNullOrEmpty(refreshToken)) return null;

                using var http = new HttpClient();
                var body = new FormUrlEncodedContent(new Dictionary<string, string>
                {
                    ["client_id"] = "697851532160-76uhho3a71cif1q0k143g22u6n7ledhf.apps.googleusercontent.com",
                    ["client_secret"] = "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",
                    ["refresh_token"] = refreshToken,
                    ["grant_type"] = "refresh_token"
                });

                var response = await http.PostAsync(
                    "https://oauth2.googleapis.com/token", body);
                var json = await response.Content.ReadAsStringAsync();

                System.Diagnostics.Debug.WriteLine($"[Auth] Token response: {json}");

                var doc = System.Text.Json.JsonDocument.Parse(json);
                var token = doc.RootElement
                               .GetProperty("access_token")
                               .GetString();

                System.Diagnostics.Debug.WriteLine(
                    $"[Auth] Fresh token obtained successfully");
                return token;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(
                    $"[Auth] Refresh failed: {ex.Message}");
                return null;
            }
        }
    }
}