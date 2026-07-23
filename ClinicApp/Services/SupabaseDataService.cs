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
                var result = await _client!.From<SupabaseBooking>().Get();

                var startUtc = date.Date.ToUniversalTime();
                var endUtc = startUtc.AddDays(1);

                return result.Models
                    .Where(b =>
                        b.AppointmentDate >= startUtc &&
                        b.AppointmentDate < endUtc &&
                        b.Status != "rejected" &&
                        b.Status != "cancelled")
                    .Select(b => b.AppointmentDate)
                    .ToList();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(
                    $"[Supabase] GetBookedSlots: {ex.Message}");
                return new List<DateTime>();
            }
        }

        public async Task RescheduleBookingAsync(string bookingId, DateTime newUtcTime)
        {
            try
            {
                await EnsureInitializedAsync();
                var result = await _client!
                    .From<SupabaseBooking>()
                    .Where(b => b.Id == bookingId)
                    .Single();

                if (result == null) return;

                result.AppointmentDate = newUtcTime;
                result.Status = "rescheduled";

                await _client!.From<SupabaseBooking>().Update(result);

                // Also update appointment_entries if exists
                var entries = await _client!
                    .From<SupabaseAppointmentEntry>()
                    .Where(e => e.SupabaseBookingId == bookingId)
                    .Get();

                var entry = entries.Models.FirstOrDefault();
                if (entry != null)
                {
                    entry.AppointmentDateTime = newUtcTime;
                    entry.Status = "rescheduled";
                    await _client!.From<SupabaseAppointmentEntry>().Update(entry);
                }

                System.Diagnostics.Debug.WriteLine(
                    $"[Supabase] Rescheduled {bookingId} to {newUtcTime:yyyy-MM-dd HH:mm} UTC");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(
                    $"[Supabase] RescheduleBooking: {ex.Message}");
                throw;
            }
        }

        public async Task<SupabasePatient?> GetPatientByPhoneAsync(string phone)
        {
            try
            {
                await EnsureInitializedAsync();
                var result = await _client!
                    .From<SupabasePatient>()
                    .Where(p => p.Phone == phone)
                    .Get();
                return result.Models.FirstOrDefault();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(
                    $"[Supabase] GetPatientByPhone: {ex.Message}");
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

        // ── Bills ─────────────────────────────────────────────────────

        public async Task<SupabaseBill?> CreateBillAsync(SupabaseBill bill)
        {
            try
            {
                await EnsureInitializedAsync();

                // Calculate balance before insert — no generated column
                bill.Balance = bill.TotalAmount - bill.AmountPaid;
                bill.BillNumber = $"B-{DateTime.Now:yyyy}-{Guid.NewGuid().ToString()[..4].ToUpper()}";

                System.Diagnostics.Debug.WriteLine(
                    $"[Supabase] Creating bill: {bill.PatientName} " +
                    $"total={bill.TotalAmount} balance={bill.Balance}");

                var result = await _client!
                    .From<SupabaseBill>()
                    .Insert(bill);

                var saved = result.Models.FirstOrDefault();
                System.Diagnostics.Debug.WriteLine(
                    $"[Supabase] Bill created: {saved?.Id ?? "NULL - INSERT FAILED"}");

                return saved;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(
                    $"[Supabase] CreateBill ERROR: {ex.Message}");
                throw;
            }
        }
        public async Task AddBillItemAsync(SupabaseBillItem item)
        {
            try
            {
                await EnsureInitializedAsync();
                await _client!.From<SupabaseBillItem>().Insert(item);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(
                    $"[Supabase] AddBillItem: {ex.Message}");
            }
        }

        public async Task<List<SupabaseBill>> GetBillsForPatientAsync(
            string patientId)
        {
            try
            {
                await EnsureInitializedAsync();
                var result = await _client!
                    .From<SupabaseBill>()
                    .Where(b => b.PatientId == patientId)
                    .Order("visit_date",
                           Supabase.Postgrest.Constants.Ordering.Descending)
                    .Get();
                return result.Models ?? new List<SupabaseBill>();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(
                    $"[Supabase] GetBillsForPatient: {ex.Message}");
                return new List<SupabaseBill>();
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
                    Id = Guid.NewGuid().ToString(),   // ← make sure Id is actually generated
                    BillId = billId,
                    Amount = amount,
                    PaymentDate = DateTime.UtcNow,
                    Notes = notes
                };
                await _client!.From<SupabasePayment>().Insert(payment);

                billResult.AmountPaid += amount;
                billResult.Balance = billResult.TotalAmount - billResult.AmountPaid;
                billResult.Status = billResult.AmountPaid >= billResult.TotalAmount
                    ? "paid" : billResult.AmountPaid > 0 ? "partial" : "unpaid";

                await _client!.From<SupabaseBill>().Update(billResult);
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
    }
}