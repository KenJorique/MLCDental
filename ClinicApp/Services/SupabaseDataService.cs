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


        // ── Google Tasks Integration ─────────────────────────────────
        public async Task<string?> SyncToGoogleTasksAsync(
                        string accessToken,
                        string patientName,
                        string service,
                        DateTime appointmentDateTime,
                        string phone,
                        string notes = "")
        {
            // Always get fresh token if empty
            if (string.IsNullOrEmpty(accessToken))
                accessToken = await GetFreshAccessTokenAsync() ?? "";

            if (string.IsNullOrEmpty(accessToken))
            {
                System.Diagnostics.Debug.WriteLine(
                    "[GoogleTasks] No token available");
                return null;
            }

            return await CallGoogleTasksApiAsync(
                accessToken, patientName, service,
                appointmentDateTime, phone, notes, false);
        }

        private async Task<string?> CallGoogleTasksApiAsync(
            string accessToken,
            string patientName,
            string service,
            DateTime appointmentDateTime,
            string phone,
            string notes,
            bool isRetry)
        {
            try
            {
                var localTime = appointmentDateTime.Kind == DateTimeKind.Utc
                    ? appointmentDateTime.ToLocalTime()
                    : appointmentDateTime;

                var task = new
                {
                    title = $"Appointment: {patientName} — {service}",
                    notes = $"Patient: {patientName}\n" +
                             $"Service: {service}\n" +
                             $"Date: {localTime:MMM dd, yyyy h:mm tt}\n" +
                             $"Phone: {phone}" +
                             (string.IsNullOrEmpty(notes)
                                 ? "" : $"\nNotes: {notes}"),
                    due = localTime.ToUniversalTime()
                                      .ToString("yyyy-MM-ddTHH:mm:ss.fffZ"),
                    status = "needsAction"
                };

                var json = System.Text.Json.JsonSerializer.Serialize(task);
                var content = new StringContent(
                    json, System.Text.Encoding.UTF8, "application/json");

                using var http = new HttpClient();
                http.Timeout = TimeSpan.FromSeconds(20);
                http.DefaultRequestHeaders.Add(
                    "Authorization", $"Bearer {accessToken}");

                var response = await http.PostAsync(
                    "https://tasks.googleapis.com/tasks/v1/lists/@default/tasks",
                    content);
                var responseText = await response.Content.ReadAsStringAsync();

                System.Diagnostics.Debug.WriteLine(
                    $"[GoogleTasks] {(int)response.StatusCode}: {responseText[..Math.Min(100, responseText.Length)]}");

                // Token expired — refresh and retry once
                if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized
                    && !isRetry)
                {
                    System.Diagnostics.Debug.WriteLine(
                        "[GoogleTasks] 401 — refreshing and retrying");
                    _cachedAccessToken = string.Empty;
                    _tokenExpiresAt = DateTime.MinValue;

                    var newToken = await GetFreshAccessTokenAsync();
                    if (string.IsNullOrEmpty(newToken)) return null;

                    return await CallGoogleTasksApiAsync(
                        newToken, patientName, service,
                        appointmentDateTime, phone, notes, true);
                }

                if (!response.IsSuccessStatusCode) return null;

                var doc = System.Text.Json.JsonDocument.Parse(responseText);
                return doc.RootElement
                          .TryGetProperty("id", out var id)
                          ? id.GetString() : null;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(
                    $"[GoogleTasks] Exception: {ex.Message}");
                return null;
            }
        }

        public async Task<bool> CompleteGoogleTaskAsync(string accessToken, string taskId)
        {
            try
            {
                if (string.IsNullOrEmpty(accessToken))
                    accessToken = await GetFreshAccessTokenAsync() ?? "";

                using var http = new HttpClient();
                http.DefaultRequestHeaders.Add("Authorization", $"Bearer {accessToken}");

                // To complete a task, we patch the status to "completed"
                var patchData = new { status = "completed" };
                var json = System.Text.Json.JsonSerializer.Serialize(patchData);
                var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");

                // Google Tasks API requires a PATCH request to update task status
                var request = new HttpRequestMessage(new HttpMethod("PATCH"),
                    $"https://tasks.googleapis.com/tasks/v1/lists/@default/tasks/{taskId}")
                {
                    Content = content
                };

                var response = await http.SendAsync(request);
                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[CompleteTask] Exception: {ex.Message}");
                return false;
            }
        }

        // Store token expiry time
        private DateTime _tokenExpiresAt = DateTime.MinValue;
        private string _cachedAccessToken = string.Empty;

        public async Task<string?> GetFreshAccessTokenAsync()
        {
            try
            {
                // Return cached token if still valid (5 min buffer)
                if (!string.IsNullOrEmpty(_cachedAccessToken)
                    && DateTime.UtcNow < _tokenExpiresAt.AddMinutes(-5))
                    return _cachedAccessToken;

                const string clientId = "697851532160-76uhho3a71cif1q0k143g22u6n7ledhf.apps.googleusercontent.com";
                const string clientSecret = "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";
                const string refreshToken = "1//0etnD-p20Px5wCgYIARAAGA4SNwF-L9IrRRqCR6LS1Egm5jBQzQycF9dM4KQ5KXD1wi8J9WHx6Yd4LWq9nd5aj0ZyZlOA1gP-wXM";

                using var http = new HttpClient();
                http.Timeout = TimeSpan.FromSeconds(30);

                var body = new FormUrlEncodedContent(new Dictionary<string, string>
                {
                    ["client_id"] = clientId,
                    ["client_secret"] = clientSecret,
                    ["refresh_token"] = refreshToken,
                    ["grant_type"] = "refresh_token"
                });

                var response = await http.PostAsync(
                    "https://oauth2.googleapis.com/token", body);
                var json = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    System.Diagnostics.Debug.WriteLine(
                        $"[Auth] Token failed: {json}");
                    return null;
                }

                var doc = System.Text.Json.JsonDocument.Parse(json);
                var accessToken = doc.RootElement
                                     .GetProperty("access_token").GetString();
                var expiresIn = doc.RootElement
                                     .TryGetProperty("expires_in", out var exp)
                                     ? exp.GetInt32() : 3600;

                _cachedAccessToken = accessToken ?? string.Empty;
                _tokenExpiresAt = DateTime.UtcNow.AddSeconds(expiresIn);

                System.Diagnostics.Debug.WriteLine("[Auth] Token refreshed successfully");
                return accessToken;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[Auth] {ex.Message}");
                return null;
            }
        }

        public async Task CleanupPastAppointmentsAsync()
        {
            try
            {
                await EnsureInitializedAsync();
                var now = DateTime.UtcNow;

                System.Diagnostics.Debug.WriteLine(
                    $"[Cleanup] Starting cleanup for appointments before {now:yyyy-MM-dd HH:mm}");

                // Get all appointment entries that are past and completed/cancelled
                var entries = await _client!
                    .From<SupabaseAppointmentEntry>()
                    .Get();

                var toDelete = entries.Models
                    .Where(e => (e.Status == "completed" || e.Status == "cancelled")
                             && e.AppointmentDateTime < now)
                    .ToList();

                System.Diagnostics.Debug.WriteLine(
                    $"[Cleanup] Found {toDelete.Count} entries to delete");

                foreach (var entry in toDelete)
                {
                    // Delete appointment entry
                    await _client!.From<SupabaseAppointmentEntry>().Delete(entry);

                    // Also delete the linked booking if it exists
                    if (!string.IsNullOrEmpty(entry.SupabaseBookingId))
                    {
                        try
                        {
                            var booking = await _client!
                                .From<SupabaseBooking>()
                                .Where(b => b.Id == entry.SupabaseBookingId)
                                .Single();

                            if (booking != null)
                                await _client!.From<SupabaseBooking>().Delete(booking);
                        }
                        catch
                        {
                            // Booking already deleted — safe to ignore
                        }
                    }

                    System.Diagnostics.Debug.WriteLine(
                        $"[Cleanup] Deleted entry for {entry.PatientName} " +
                        $"({entry.Status}) on {entry.AppointmentDateTime:MMM dd}");
                }

                System.Diagnostics.Debug.WriteLine(
                    $"[Cleanup] Done. {toDelete.Count} entries cleaned up.");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(
                    $"[Cleanup] Error: {ex.Message}");
            }
        }

        // ── Treatment Records ─────────────────────────────────────────

        public async Task<SupabaseTreatmentRecord?> AddTreatmentRecordAsync(
            SupabaseTreatmentRecord record)
        {
            try
            {
                await EnsureInitializedAsync();
                var result = await _client!
                    .From<SupabaseTreatmentRecord>()
                    .Insert(record);
                return result.Models.FirstOrDefault();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(
                    $"[Supabase] AddTreatmentRecord: {ex.Message}");
                return null;
            }
        }

        public async Task<List<SupabaseTreatmentRecord>> GetTreatmentRecordsAsync(
            string patientId)
        {
            try
            {
                await EnsureInitializedAsync();
                var result = await _client!
                    .From<SupabaseTreatmentRecord>()
                    .Where(r => r.PatientId == patientId)
                    .Order("visit_date",
                           Supabase.Postgrest.Constants.Ordering.Descending)
                    .Get();
                return result.Models ?? new List<SupabaseTreatmentRecord>();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(
                    $"[Supabase] GetTreatmentRecords: {ex.Message}");
                return new List<SupabaseTreatmentRecord>();
            }
        }

        // ── Transactions ──────────────────────────────────────────────

        public async Task<SupabaseTransaction?> AddTransactionAsync(
            SupabaseTransaction transaction)
        {
            try
            {
                await EnsureInitializedAsync();
                var result = await _client!
                    .From<SupabaseTransaction>()
                    .Insert(transaction);
                return result.Models.FirstOrDefault();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(
                    $"[Supabase] AddTransaction: {ex.Message}");
                return null;
            }
        }

        public async Task<List<SupabaseTransaction>> GetTransactionsAsync(
            string patientId)
        {
            try
            {
                await EnsureInitializedAsync();
                var result = await _client!
                    .From<SupabaseTransaction>()
                    .Where(t => t.PatientId == patientId)
                    .Order("created_at",
                           Supabase.Postgrest.Constants.Ordering.Descending)
                    .Get();
                return result.Models ?? new List<SupabaseTransaction>();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(
                    $"[Supabase] GetTransactions: {ex.Message}");
                return new List<SupabaseTransaction>();
            }
        }

        public async Task<List<SupabaseTransaction>> GetUnpaidTransactionsAsync()
        {
            try
            {
                await EnsureInitializedAsync();
                var result = await _client!
                    .From<SupabaseTransaction>()
                    .Where(t => t.PaymentStatus != "paid")
                    .Order("created_at",
                           Supabase.Postgrest.Constants.Ordering.Descending)
                    .Get();
                return result.Models ?? new List<SupabaseTransaction>();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(
                    $"[Supabase] GetUnpaidTransactions: {ex.Message}");
                return new List<SupabaseTransaction>();
            }
        }

        public async Task<bool> RecordTransactionPaymentAsync(
            string transactionId, decimal amountToPay)
        {
            try
            {
                await EnsureInitializedAsync();

                var result = await _client!
                    .From<SupabaseTransaction>()
                    .Where(t => t.Id == transactionId)
                    .Single();

                if (result == null) return false;

                result.AmountPaid += amountToPay;
                result.PaymentDate = DateTime.UtcNow;
                result.PaymentStatus = result.AmountPaid >= result.TotalAmount
                    ? "paid" : "partial";

                await _client!.From<SupabaseTransaction>().Update(result);
                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(
                    $"[Supabase] RecordPayment: {ex.Message}");
                return false;
            }
        }

        public async Task<List<DateTime>> GetBookedTimeSlotsForDateAsync(DateTime date)
        {
            try
            {
                await EnsureInitializedAsync();

                var result = await _client!
                    .From<SupabaseAppointmentEntry>()
                    .Get();
                System.Diagnostics.Debug.WriteLine(
    $"Appointment Entries Count = {result.Models.Count}");

                foreach (var a in result.Models)
                {
                    System.Diagnostics.Debug.WriteLine(
                        $"{a.PatientName} | {a.AppointmentDateTime:o} | {a.Status}");
                }
                return result.Models
                    .Where(x =>
                    {
                        var local = x.AppointmentDateTime.ToLocalTime();

                        return local.Date == date.Date &&
                               x.Status != "cancelled" &&
                               x.Status != "completed" &&
                               x.Status != "rejected";
                    })
                    .Select(x => x.AppointmentDateTime)
                    .ToList();

            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(ex);

                return new List<DateTime>();
            }
        }

        public async Task<bool> IsSlotAvailableAsync(DateTime utcTime)
        {
            await EnsureInitializedAsync();

            var result = await _client!
                .From<SupabaseAppointmentEntry>()
                .Get();
            System.Diagnostics.Debug.WriteLine(
    $"Appointment Entries Count = {result.Models.Count}");

            foreach (var a in result.Models)
            {
                System.Diagnostics.Debug.WriteLine(
                    $"{a.PatientName} | {a.AppointmentDateTime:o} | {a.Status}");
            }
            return !result.Models.Any(a =>
            {
                var dt = a.AppointmentDateTime.ToUniversalTime();

                return dt.Year == utcTime.Year &&
                       dt.Month == utcTime.Month &&
                       dt.Day == utcTime.Day &&
                       dt.Hour == utcTime.Hour &&
                       dt.Minute == utcTime.Minute &&
                       a.Status != "cancelled" &&
                       a.Status != "completed" &&
                       a.Status != "rejected";
            });
        }

        public async Task RescheduleBookingAsync(
    string appointmentEntryId,
    DateTime newUtcTime)
        {
            try
            {
                await EnsureInitializedAsync();

                var result = await _client!
                    .From<SupabaseAppointmentEntry>()
                    .Where(x => x.Id == appointmentEntryId)
                    .Single();

                if (result == null)
                    return;

                result.AppointmentDateTime = newUtcTime;
                result.Status = "rescheduled";

                await _client!
                    .From<SupabaseAppointmentEntry>()
                    .Update(result);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(
                    $"[Supabase] Reschedule: {ex.Message}");

                throw;
            }
        }

        public async Task<SupabasePatient?> GetPatientByPhoneAsync(string phone)
        {
            try
            {
                await EnsureInitializedAsync();

                var digitsOnly = new string((phone ?? "").Where(char.IsDigit).ToArray());
                if (string.IsNullOrEmpty(digitsOnly))
                    return null;

                var result = await _client!.From<SupabasePatient>().Get();
                var patients = result.Models ?? new List<SupabasePatient>();

                return patients.FirstOrDefault(p =>
                    !string.IsNullOrEmpty(p.Phone) &&
                    new string(p.Phone.Where(char.IsDigit).ToArray())
                        .EndsWith(digitsOnly.Length >= 7 ? digitsOnly[^7..] : digitsOnly));
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[Supabase] GetPatientByPhone: {ex.Message}");
                return null;
            }
        }

        // ── Services ──────────────────────────────────────────────────

        public async Task<List<SupabaseService>> GetServicesAsync()
        {
            try
            {
                await EnsureInitializedAsync();
                var result = await _client!
                    .From<SupabaseService>()
                    .Where(s => s.IsActive == true)
                    .Order("name", Supabase.Postgrest.Constants.Ordering.Ascending)
                    .Get();
                return result.Models ?? new List<SupabaseService>();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(
                    $"[Supabase] GetServices: {ex.Message}");
                return new List<SupabaseService>();
            }
        }

        public async Task<SupabaseService?> AddServiceAsync(SupabaseService service)
        {
            try
            {
                await EnsureInitializedAsync();
                var result = await _client!.From<SupabaseService>().Insert(service);
                return result.Models.FirstOrDefault();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[Supabase] AddService: {ex.Message}");
                return null;
            }
        }

        public async Task<bool> UpdateServiceAsync(SupabaseService service)
        {
            try
            {
                await EnsureInitializedAsync();

                if (string.IsNullOrEmpty(service.Id))
                {
                    System.Diagnostics.Debug.WriteLine("[Supabase] UpdateService: Id is empty — cannot update");
                    return false;
                }

                var result = await _client!.From<SupabaseService>().Update(service);
                System.Diagnostics.Debug.WriteLine($"[Supabase] UpdateService done. Rows: {result.Models.Count}");
                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[Supabase] UpdateService FAILED: {ex.Message}");
                return false;
            }
        }

        // Soft delete — flips is_active to false instead of removing the row
        public async Task<bool> DeleteServiceAsync(string serviceId)
        {
            try
            {
                await EnsureInitializedAsync();
                await _client!
                    .From<SupabaseService>()
                    .Where(s => s.Id == serviceId)
                    .Set(s => s.IsActive, false)
                    .Update();
                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[Supabase] DeleteService FAILED: {ex.Message}");
                return false;
            }
        }

        // ── Bills ─────────────────────────────────────────────────────

        public async Task<SupabaseBill?> CreateBillAsync(SupabaseBill bill)
        {
            await EnsureInitializedAsync();

            bill.Balance = bill.TotalAmount - bill.AmountPaid;
            bill.BillNumber = $"B-{DateTime.Now:yyyy}-{Guid.NewGuid().ToString()[..4].ToUpper()}";


            System.Diagnostics.Debug.WriteLine("===== INSERTING BILL =====");

            var result = await _client!
                .From<SupabaseBill>()
                .Insert(bill);

            System.Diagnostics.Debug.WriteLine($"Models Count = {result.Models.Count}");

            foreach (var b in result.Models)
            {
                System.Diagnostics.Debug.WriteLine($"Returned Id = {b.Id}");
                System.Diagnostics.Debug.WriteLine($"Returned BillNo = {b.BillNumber}");
            }

            return result.Models.FirstOrDefault();
        }
        public async Task AddBillItemAsync(SupabaseBillItemInsert item)
        {
            await EnsureInitializedAsync();

            try
            {
                var result = await _client!
                    .From<SupabaseBillItemInsert>()
                    .Insert(item);


                System.Diagnostics.Debug.WriteLine(
                    $"[Supabase] Bill item saved: {item.ServiceName}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(
                    $"[Supabase] AddBillItem ERROR: {ex}");

                throw;
            }
        }

        public async Task<SupabaseBill?> GetBillByIdAsync(string billId)
        {
            try
            {
                await EnsureInitializedAsync();
                return await _client!
                    .From<SupabaseBill>()
                    .Where(b => b.Id == billId)
                    .Single();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[Supabase] GetBillById: {ex.Message}");
                return null;
            }
        }

        public async Task<List<SupabaseBill>> GetBillsForPatientAsync(string patientId)
        {
            try
            {
                await EnsureInitializedAsync();

                var result = await _client!
                    .From<SupabaseBill>()
                    .Order("visit_date", Supabase.Postgrest.Constants.Ordering.Descending)
                    .Get();

                var bills = result.Models ?? new List<SupabaseBill>();

                var matches = bills.Where(b => b.PatientId == patientId).ToList();
                if (matches.Any())
                    return matches;

                // Walk-in fallback: match by patient name instead of ID
                var patientResult = await _client!
                    .From<SupabasePatient>()
                    .Where(p => p.Id == patientId)
                    .Get();

                var patient = patientResult.Models?.FirstOrDefault();
                if (patient == null)
                    return new List<SupabaseBill>();

                var fullName = $"{patient.FirstName} {patient.LastName}".Trim();

                return bills
                    .Where(b => string.Equals(b.PatientName?.Trim(), fullName, StringComparison.OrdinalIgnoreCase))
                    .ToList();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[Supabase] GetBillsForPatient: {ex.Message}");
                return new List<SupabaseBill>();
            }
        }
        public async Task<SupabaseBooking?> AddBookingAsync(SupabaseBooking booking)
        {
            try
            {
                await EnsureInitializedAsync();

                var result = await _client!
                    .From<SupabaseBooking>()
                    .Insert(booking);

                return result.Models.FirstOrDefault();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(
                    $"[Supabase] AddBooking: {ex.Message}");

                return null;
            }
        }


        public async Task<List<SupabaseBillItem>> GetBillItemsAsync(string billId)
        {
            try
            {
                await EnsureInitializedAsync();
                var result = await _client!
                    .From<SupabaseBillItem>()
                    .Where(i => i.BillId == billId)
                    .Get();
                return result.Models ?? new List<SupabaseBillItem>();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(
                    $"[Supabase] GetBillItems: {ex.Message}");
                return new List<SupabaseBillItem>();
            }
        }

        public async Task<List<SupabaseBill>> GetUnpaidBillsAsync()
        {
            try
            {
                await EnsureInitializedAsync();
                var result = await _client!
                    .From<SupabaseBill>()
                    .Where(b => b.Status != "paid")
                    .Order("visit_date",
                           Supabase.Postgrest.Constants.Ordering.Descending)
                    .Get();
                return result.Models ?? new List<SupabaseBill>();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(
                    $"[Supabase] GetUnpaidBills: {ex.Message}");
                return new List<SupabaseBill>();
            }
        }

        public async Task<(bool Success, string? Error)> RecordPaymentAsync(
     string billId, decimal amount, string? notes = null)
        {
            try
            {
                await EnsureInitializedAsync();
                var billResult = await _client!
                    .From<SupabaseBill>()
                    .Where(b => b.Id == billId)
                    .Single();

                if (billResult == null)
                    return (false, "Bill not found");

                var payment = new SupabasePayment
                {
                    Id = Guid.NewGuid().ToString(),
                    BillId = billId,
                    Amount = amount,
                    PaymentDate = DateTime.UtcNow,
                    Notes = notes
                };
                await _client!.From<SupabasePayment>().Insert(payment);

                billResult.AmountPaid += amount;
                billResult.Balance = billResult.TotalAmount - billResult.AmountPaid;
                billResult.LastPaymentDate = payment.PaymentDate;

                if (billResult.AmountPaid >= billResult.TotalAmount)
                {
                    billResult.Status = "paid";
                    billResult.DueDate = null;
                }
                else
                {
                    billResult.Status = billResult.AmountPaid > 0 ? "partial" : "unpaid";

                    if (billResult.IsInstallment)
                        billResult.DueDate = payment.PaymentDate.AddMonths(1);
                }

                var updateResult = await _client!.From<SupabaseBill>().Update(billResult);

                System.Diagnostics.Debug.WriteLine(
                    $"[Supabase] RecordPayment UPDATE rows returned: {updateResult.Models.Count}");

                if (updateResult.Models.Count == 0)
                {
                    System.Diagnostics.Debug.WriteLine(
                        "[Supabase] WARNING: Update affected 0 rows — check RLS UPDATE policy on 'bills' table.");
                    return (false, "Payment saved but bill status wasn't updated. Check permissions.");
                }

                return (true, null);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[Supabase] RecordPayment: {ex.Message}");
                return (false, ex.Message);
            }
        }
        public async Task<List<SupabasePayment>> GetPaymentsForBillAsync(
            string billId)
        {
            try
            {
                await EnsureInitializedAsync();
                var result = await _client!
                    .From<SupabasePayment>()
                    .Where(p => p.BillId == billId)
                    .Order("payment_date",
                           Supabase.Postgrest.Constants.Ordering.Descending)
                    .Get();
                return result.Models ?? new List<SupabasePayment>();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(
                    $"[Supabase] GetPaymentsForBill: {ex.Message}");
                return new List<SupabasePayment>();
            }
        }

        public async Task<List<SupabaseBill>> GetAllBillsAsync()
        {
            try
            {
                await EnsureInitializedAsync();
                var result = await _client!
                    .From<SupabaseBill>()
                    .Order("visit_date",
                           Supabase.Postgrest.Constants.Ordering.Descending)
                    .Get();
                return result.Models ?? new List<SupabaseBill>();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(
                    $"[Supabase] GetAllBills: {ex.Message}");
                return new List<SupabaseBill>();
            }
        }

        // ── Supplies ──────────────────────────────────────────────

        public async Task<List<SupabaseSupplyItem>> GetSuppliesAsync()
        {
            try
            {
                await EnsureInitializedAsync();
                var result = await _client!
                    .From<SupabaseSupplyItem>()
                    .Where(s => s.IsDeleted == false)
                    .Get();
                return result.Models ?? new List<SupabaseSupplyItem>();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[Supabase] GetSupplies: {ex.Message}");
                return new List<SupabaseSupplyItem>();
            }
        }

        public async Task<SupabaseSupplyItem?> GetSupplyByIdAsync(string id)
        {
            try
            {
                await EnsureInitializedAsync();
                return await _client!
                    .From<SupabaseSupplyItem>()
                    .Where(s => s.Id == id)
                    .Single();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[Supabase] GetSupplyById: {ex.Message}");
                return null;
            }
        }

        public async Task<SupabaseSupplyItem?> AddSupplyAsync(SupabaseSupplyItem supply)
        {
            try
            {
                await EnsureInitializedAsync();
                var result = await _client!.From<SupabaseSupplyItem>().Insert(supply);
                return result.Models.FirstOrDefault();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[Supabase] AddSupply: {ex.Message}");
                return null;
            }
        }

        public async Task<bool> UpdateSupplyAsync(SupabaseSupplyItem supply)
        {
            try
            {
                await EnsureInitializedAsync();
                if (string.IsNullOrEmpty(supply.Id)) return false;
                await _client!.From<SupabaseSupplyItem>().Update(supply);
                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[Supabase] UpdateSupply FAILED: {ex.Message}");
                return false;
            }
        }

        // Soft delete
        public async Task<bool> DeleteSupplyAsync(string supplyId)
        {
            try
            {
                await EnsureInitializedAsync();
                await _client!
                    .From<SupabaseSupplyItem>()
                    .Where(s => s.Id == supplyId)
                    .Set(s => s.IsDeleted, true)
                    .Update();
                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[Supabase] DeleteSupply FAILED: {ex.Message}");
                return false;
            }
        }

        // Applies a stock delta (+ restock / − used) and writes a log row
        public async Task<bool> ApplyStockChangeAsync(
            string supplyId, int changeInPieces, string changeType, string note,
            string? patientId = null, string? patientName = null)
        {
            try
            {
                await EnsureInitializedAsync();

                var supply = await GetSupplyByIdAsync(supplyId);
                if (supply is null) return false;

                int newQty = supply.QuantityInPieces + changeInPieces;
                if (newQty < 0) newQty = 0;

                supply.QuantityInPieces = newQty;
                var updated = await UpdateSupplyAsync(supply);
                if (!updated) return false;

                await _client!.From<SupabaseStockLog>().Insert(new SupabaseStockLog
                {
                    SupplyId = supplyId,
                    ChangeType = changeType,
                    ChangeInPieces = changeInPieces,
                    StockAfterChange = newQty,
                    PatientId = patientId,
                    PatientName = patientName,
                    Note = note,
                    CreatedAt = DateTime.UtcNow
                });

                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[Supabase] ApplyStockChange FAILED: {ex.Message}");
                return false;
            }
        }

        public async Task<List<SupabaseStockLog>> GetLogsForSupplyAsync(string supplyId)
        {
            try
            {
                await EnsureInitializedAsync();
                var result = await _client!
                    .From<SupabaseStockLog>()
                    .Where(l => l.SupplyId == supplyId)
                    .Order("created_at", Supabase.Postgrest.Constants.Ordering.Descending)
                    .Get();
                return result.Models ?? new List<SupabaseStockLog>();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[Supabase] GetLogsForSupply: {ex.Message}");
                return new List<SupabaseStockLog>();
            }
        }

        // ── Service → Supply linking ─────────────────────────────

        public async Task<List<SupabaseServiceSupply>> GetSuppliesForServiceAsync(string serviceId)
        {
            try
            {
                await EnsureInitializedAsync();
                var result = await _client!
                    .From<SupabaseServiceSupply>()
                    .Where(x => x.ServiceId == serviceId)
                    .Get();
                return result.Models ?? new List<SupabaseServiceSupply>();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[Supabase] GetSuppliesForService: {ex.Message}");
                return new List<SupabaseServiceSupply>();
            }
        }

        /// <summary>
        /// Deducts every supply linked to a service from stock and logs each deduction.
        /// Call this once, when a service is actually performed/completed on a patient
        /// — not when it's merely selected or billed.
        /// Returns Success=false if the service has no linked supplies, or if any
        /// linked item's stock is now insufficient (InsufficientStock lists their names —
        /// the deduction still goes through and clamps at 0, this is just a heads-up).
        /// </summary>
        public async Task<(bool Success, List<string> InsufficientStock)> DeductSuppliesForServiceAsync(
    string serviceId, string? patientId = null, string? patientName = null, int quantity = 1)
        {
            var insufficient = new List<string>();
            try
            {
                await EnsureInitializedAsync();
                var links = await GetSuppliesForServiceAsync(serviceId);
                if (links.Count == 0) return (true, insufficient);

                foreach (var link in links)
                {
                    var supply = await GetSupplyByIdAsync(link.SupplyId);
                    if (supply is null) continue;

                    int qtyToDeduct = Math.Max(1, (int)Math.Ceiling(link.QuantityUsed)) * Math.Max(1, quantity);

                    if (supply.QuantityInPieces < qtyToDeduct)
                        insufficient.Add(supply.Name);

                    await ApplyStockChangeAsync(
                        supply.Id, -qtyToDeduct, "Used",
                        "Auto-deducted from service", patientId, patientName);
                }

                return (insufficient.Count == 0, insufficient);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[Supabase] DeductSuppliesForService FAILED: {ex.Message}");
                return (false, insufficient);
            }
        }
    }
}