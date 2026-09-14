using ClinicApp.Models.AppointmentModels;
using ClinicApp.Models.PatientModels;
using ClinicApp.Models.SupabaseModels;
using ClinicApp.Helpers;
using Supabase;

namespace ClinicApp.Services
{
    public partial class SupabaseDataService
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

        // Fetches one patient by their Supabase id — used to merge partial edits onto the full record before updating.
        public async Task<SupabasePatient?> GetPatientByIdAsync(string id)
        {
            try
            {
                await EnsureInitializedAsync();
                if (string.IsNullOrEmpty(id)) return null;

                var result = await _client!.From<SupabasePatient>().Where(p => p.Id == id).Get();
                return result.Models.FirstOrDefault();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[Supabase] GetPatientById: {ex.Message}");
                return null;
            }
        }

        // Overwrites the FULL row — callers must merge onto the existing record first, or unset fields get blanked.
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

                // A 0-row result means RLS silently blocked it (or the row's gone) — Postgres doesn't
                // throw for that, so without this check the caller would wrongly believe it worked.
                if (result.Models.Count == 0)
                {
                    System.Diagnostics.Debug.WriteLine("[Supabase] UpdatePatient: 0 rows affected — check RLS UPDATE policy on 'patients'");
                    return false;
                }

                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[Supabase] UpdatePatient FAILED: {ex.Message}");
                return false;
            }
        }

        // Cascades through every table that references this patient before deleting the row itself,
        // so nothing gets orphaned (bills/items/payments, transactions, treatment records/history,
        // tooth records, treatment sequences).
        public async Task DeletePatientAsync(SupabasePatient patient)
        {
            try
            {
                await EnsureInitializedAsync();

                var patientId = patient.Id;

                // ── Bills + their line items + payments ──
                var billsResult = await _client!
                    .From<SupabaseBill>()
                    .Where(b => b.PatientId == patientId)
                    .Get();

                foreach (var bill in billsResult.Models ?? new List<SupabaseBill>())
                {
                    var itemsResult = await _client!
                        .From<SupabaseBillItem>()
                        .Where(i => i.BillId == bill.Id)
                        .Get();
                    foreach (var item in itemsResult.Models ?? new List<SupabaseBillItem>())
                        await _client!.From<SupabaseBillItem>().Delete(item);

                    var paymentsResult = await _client!
                        .From<SupabasePayment>()
                        .Where(p => p.BillId == bill.Id)
                        .Get();
                    foreach (var payment in paymentsResult.Models ?? new List<SupabasePayment>())
                        await _client!.From<SupabasePayment>().Delete(payment);

                    await _client!.From<SupabaseBill>().Delete(bill);
                }

                // ── Transactions ──
                var txResult = await _client!
                    .From<SupabaseTransaction>()
                    .Where(t => t.PatientId == patientId)
                    .Get();
                foreach (var tx in txResult.Models ?? new List<SupabaseTransaction>())
                    await _client!.From<SupabaseTransaction>().Delete(tx);

                // ── Treatment records ──
                var trResult = await _client!
                    .From<SupabaseTreatmentRecord>()
                    .Where(r => r.PatientId == patientId)
                    .Get();
                foreach (var r in trResult.Models ?? new List<SupabaseTreatmentRecord>())
                    await _client!.From<SupabaseTreatmentRecord>().Delete(r);

                // ── Treatment history ──
                var thResult = await _client!
                    .From<SupabaseTreatmentHistory>()
                    .Where(h => h.PatientId == patientId)
                    .Get();
                foreach (var h in thResult.Models ?? new List<SupabaseTreatmentHistory>())
                    await _client!.From<SupabaseTreatmentHistory>().Delete(h);

                // ── Tooth records ──
                var toothResult = await _client!
                    .From<SupabaseToothRecord>()
                    .Where(r => r.PatientId == patientId)
                    .Get();
                foreach (var r in toothResult.Models ?? new List<SupabaseToothRecord>())
                    await _client!.From<SupabaseToothRecord>().Delete(r);

                // ── Treatment sequences ──
                var seqResult = await _client!
                    .From<SupabaseTreatmentSequence>()
                    .Where(t => t.PatientId == patientId)
                    .Get();
                foreach (var s in seqResult.Models ?? new List<SupabaseTreatmentSequence>())
                    await _client!.From<SupabaseTreatmentSequence>().Delete(s);

                // ── Finally, the patient itself ──
                await _client!.From<SupabasePatient>().Delete(patient);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[Supabase] DeletePatient: {ex.Message}");
            }
        }

        // ── Medical Conditions (normalized) ────────────
        public async Task<List<SupabaseMedicalCondition>> GetMedicalConditionsAsync()
        {
            try
            {
                await EnsureInitializedAsync();
                var result = await _client!.From<SupabaseMedicalCondition>().Get();
                return result.Models ?? new List<SupabaseMedicalCondition>();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[Supabase] GetMedicalConditions: {ex.Message}");
                return new List<SupabaseMedicalCondition>();
            }
        }

        public async Task<List<SupabasePatientCondition>> GetPatientConditionsAsync(string patientId)
        {
            try
            {
                await EnsureInitializedAsync();
                var result = await _client!
                    .From<SupabasePatientCondition>()
                    .Where(pc => pc.PatientId == patientId)
                    .Get();
                return result.Models ?? new List<SupabasePatientCondition>();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[Supabase] GetPatientConditions: {ex.Message}");
                return new List<SupabasePatientCondition>();
            }
        }

        /// Replaces the patient's full condition set — remove then re-insert,
        /// mirroring DatabaseService.SavePatientConditions' remove/re-add pattern.
        public async Task SavePatientConditionsAsync(string patientId, List<long> conditionIds)
        {
            try
            {
                await EnsureInitializedAsync();

                var existing = await GetPatientConditionsAsync(patientId);
                foreach (var row in existing)
                    await _client!.From<SupabasePatientCondition>().Delete(row);

                foreach (var conditionId in conditionIds)
                    await _client!.From<SupabasePatientCondition>().Insert(new SupabasePatientCondition
                    {
                        PatientId = patientId,
                        ConditionId = conditionId
                    });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[Supabase] SavePatientConditions: {ex.Message}");
            }
        }

        // ── Guardians (shared across siblings) ─────────
        /// Looks for an existing guardian first by mobile (a decent natural
        /// key for a person), falling back to an exact name match if no
        /// mobile is on file. Returns null if nothing matches.
        public async Task<SupabaseGuardian?> FindGuardianAsync(string? name, string? mobile)
        {
            try
            {
                await EnsureInitializedAsync();

                if (!string.IsNullOrWhiteSpace(mobile))
                {
                    var byMobile = await _client!
                        .From<SupabaseGuardian>()
                        .Where(g => g.Mobile == mobile)
                        .Get();
                    var match = byMobile.Models?.FirstOrDefault();
                    if (match != null) return match;
                }

                if (!string.IsNullOrWhiteSpace(name))
                {
                    var byName = await _client!
                        .From<SupabaseGuardian>()
                        .Where(g => g.Name == name)
                        .Get();
                    return byName.Models?.FirstOrDefault();
                }

                return null;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[Supabase] FindGuardian: {ex.Message}");
                return null;
            }
        }

        public async Task<SupabaseGuardian?> AddGuardianAsync(SupabaseGuardian g)
        {
            try
            {
                await EnsureInitializedAsync();
                var result = await _client!.From<SupabaseGuardian>().Insert(g);
                return result.Models?.FirstOrDefault();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[Supabase] AddGuardian: {ex.Message}");
                return null;
            }
        }

        public async Task<bool> UpdateGuardianAsync(SupabaseGuardian g)
        {
            try
            {
                await EnsureInitializedAsync();
                await _client!.From<SupabaseGuardian>().Update(g);
                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[Supabase] UpdateGuardian: {ex.Message}");
                return false;
            }
        }

        public async Task<SupabaseGuardian?> GetGuardianByIdAsync(long id)
        {
            try
            {
                await EnsureInitializedAsync();
                var result = await _client!.From<SupabaseGuardian>().Where(g => g.Id == id).Get();
                return result.Models?.FirstOrDefault();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[Supabase] GetGuardianById: {ex.Message}");
                return null;
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

        // Fetches a single booking by its id — used when a caller only has the id (e.g. rescheduling a pending booking).
        public async Task<SupabaseBooking?> GetBookingByIdAsync(string bookingId)
        {
            try
            {
                await EnsureInitializedAsync();
                var result = await _client!
                    .From<SupabaseBooking>()
                    .Where(b => b.Id == bookingId)
                    .Single();
                return result;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[Supabase] GetBookingById: {ex.Message}");
                return null;
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

        // Approves a booking at the given local (Manila) date/time: resolves/creates the patient (Supabase phone match, then name match, then local-DB fallback), guards against a double-booked slot, creates the appointment entry (local + Supabase), marks the booking approved, and syncs Google Tasks. Shared by AppointmentViewModel.Approve() and RescheduleViewModel (for rescheduling a booking that was never approved yet).
        public async Task<(bool Success, string? ErrorMessage)> ApproveBookingAsync(
            DatabaseService db, SupabaseBooking booking, DateTime localDateTime)
        {
            try
            {
                SupabasePatient? patient = null;
                Patient? localOnlyMatch = null;

                if (!string.IsNullOrEmpty(booking.Phone))
                    patient = await GetPatientByPhoneAsync(booking.Phone);

                if (patient == null && !string.IsNullOrEmpty(booking.FullName))
                    patient = await GetPatientByNameAsync(booking.FullName);

                if (patient == null)
                {
                    var localPatients = await db.GetPatients();
                    var bookingTokens = TokenizeName(booking.FullName ?? "");
                    localOnlyMatch = localPatients.FirstOrDefault(p =>
                        NamesMatch(bookingTokens, TokenizeName(p.FullName)) ||
                        (!string.IsNullOrEmpty(booking.Phone) && PhoneEndsMatch(p.MobileNo, booking.Phone)));
                }

                if (patient == null && localOnlyMatch == null)
                {
                    // No match anywhere — genuinely a new patient.
                    var parts = (booking.FullName ?? "").Trim().Split(' ', 2);
                    var localPatient = new Patient
                    {
                        FirstName = parts.Length > 0 ? parts[0] : "",
                        LastName = parts.Length > 1 ? parts[1] : "",
                        MobileNo = booking.Phone ?? "",
                        Email = booking.Email ?? "",
                        ReferredBy = "Online Booking",
                        DateRegistered = DateTime.Now.ToString("yyyy-MM-dd")
                    };

                    var supPatient = new SupabasePatient
                    {
                        FirstName = localPatient.FirstName,
                        LastName = localPatient.LastName,
                        Phone = localPatient.MobileNo,
                        Email = localPatient.Email,
                        ReferredBy = "Online Booking",
                        DateRegistered = DateTime.UtcNow
                    };
                    patient = await AddPatientAsync(supPatient);

                    if (patient != null)
                        localPatient.SupabaseId = patient.Id;

                    await db.AddPatient(localPatient);
                }
                else if (patient != null)
                {
                    // Matched on Supabase — overwrite the phone there and locally if this booking used a different number.
                    if (!string.IsNullOrEmpty(booking.Phone) && patient.Phone != booking.Phone)
                    {
                        patient.Phone = booking.Phone;
                        await UpdatePatientAsync(patient);
                        await db.SyncPatientFromSupabase(patient);
                    }
                }
                else if (localOnlyMatch != null)
                {
                    // Found locally but never made it to Supabase — sync it up now.
                    if (!string.IsNullOrEmpty(booking.Phone))
                        localOnlyMatch.MobileNo = booking.Phone;

                    var supPatient = new SupabasePatient
                    {
                        FirstName = localOnlyMatch.FirstName,
                        LastName = localOnlyMatch.LastName,
                        Phone = localOnlyMatch.MobileNo,
                        Email = localOnlyMatch.Email,
                        ReferredBy = localOnlyMatch.ReferredBy,
                        DateRegistered = DateTime.UtcNow
                    };
                    var inserted = await AddPatientAsync(supPatient);

                    if (inserted != null)
                    {
                        localOnlyMatch.SupabaseId = inserted.Id;
                        patient = inserted;
                    }

                    await db.UpdatePatient(localOnlyMatch);
                }

                // Convert the picked local (Manila) time to UTC explicitly, then guard against a slot taken since it was offered.
                var utcDate = TimeZoneInfo.ConvertTimeToUtc(
                    DateTime.SpecifyKind(localDateTime, DateTimeKind.Unspecified), ManilaTz);

                var slotStillFree = await IsSlotAvailableAsync(utcDate);
                if (!slotStillFree)
                {
                    return (false,
                        $"{localDateTime:MMM dd, yyyy h:mm tt} has already been booked by another approved appointment. " +
                        "Please choose a different time.");
                }

                var localEntry = new AppointmentEntry
                {
                    SupabaseBookingId = booking.Id,
                    PatientName = booking.FullName ?? "",
                    Phone = booking.Phone ?? "",
                    Email = booking.Email ?? "",
                    Notes = booking.Notes ?? "",
                    AppointmentDateTime = localDateTime.ToString("yyyy-MM-dd HH:mm:ss"),
                    Status = "approved"
                };
                await db.AddAppointmentEntry(localEntry);

                var supEntry = new SupabaseAppointmentEntry
                {
                    SupabaseBookingId = booking.Id,
                    PatientId = patient?.Id ?? "",
                    PatientName = booking.FullName ?? "",
                    Phone = booking.Phone ?? "",
                    Email = booking.Email ?? "",
                    Notes = booking.Notes ?? "",
                    AppointmentDateTime = utcDate,
                    Status = "approved"
                };
                await AddAppointmentEntryAsync(supEntry);

                await UpdateBookingStatusAsync(booking.Id, "approved");

                try
                {
                    await SyncToGoogleTasksAsync(
                        "", booking.FullName ?? "", " ", localDateTime, booking.Phone ?? "", booking.Notes ?? "");
                }
                catch (Exception gEx)
                {
                    System.Diagnostics.Debug.WriteLine($"[ApproveBooking] Google: {gEx.Message}");
                }

                return (true, null);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ApproveBooking] {ex.Message}");
                return (false, ex.Message);
            }
        }

        // Compares two phone numbers by their last 7 digits, to tolerate formatting differences.
        private static bool PhoneEndsMatch(string a, string b)
        {
            var digitsA = new string((a ?? "").Where(char.IsDigit).ToArray());
            var digitsB = new string((b ?? "").Where(char.IsDigit).ToArray());
            if (digitsA.Length == 0 || digitsB.Length == 0) return false;

            var tailA = digitsA.Length >= 7 ? digitsA[^7..] : digitsA;
            var tailB = digitsB.Length >= 7 ? digitsB[^7..] : digitsB;
            return tailA == tailB;
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
                const string refreshToken = "1//04lNOw9Ik3RmfCgYIARAAGAQSNwF-L9IrWCDoRUW-BrnhpvGtUQvPJykV5kJQT-epjT75UhGphOTNb1Xr7wVCRE3XuNKKE8vY458";

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

        static readonly TimeZoneInfo ManilaTz =
    TimeZoneInfo.FindSystemTimeZoneById("Asia/Manila") ?? TimeZoneInfo.Utc;

        static readonly int[] BusinessHours = { 10, 11, 13, 14, 15, 16 };

        // Old bookings stored the Manila hour directly, mislabeled as UTC; newer bookings store genuine UTC.
        // The two interpretations never overlap for this clinic's fixed hours, so the raw hour alone tells
        // us which format a given row is in — no data migration needed, this just reads each row correctly.
        // Kept under its original name since AppointmentScheduleViewModel and RescheduleViewModel call it directly.
        public static DateTime NormalizeSupabaseUtc(DateTime deserializedValue)
        {
            if (BusinessHours.Contains(deserializedValue.Hour))
            {
                // Raw value IS the Manila local time (old format) — convert it to true UTC.
                var asManilaLocal = DateTime.SpecifyKind(deserializedValue, DateTimeKind.Unspecified);
                return TimeZoneInfo.ConvertTimeToUtc(asManilaLocal, ManilaTz);
            }

            // Raw value already IS true UTC (new format) — just fix the Kind tag.
            return DateTime.SpecifyKind(deserializedValue, DateTimeKind.Utc);
        }

        public async Task<List<DateTime>> GetBookedTimeSlotsForDateAsync(DateTime date)
        {
            try
            {
                await EnsureInitializedAsync();
                var result = await _client!
                    .From<SupabaseAppointmentEntry>()
                    .Get();

                return result.Models
                    .Where(x =>
                    {
                        if (x.Status == "cancelled" || x.Status == "completed" || x.Status == "rejected")
                            return false;

                        var utc = NormalizeSupabaseUtc(x.AppointmentDateTime);
                        var localPh = TimeZoneInfo.ConvertTimeFromUtc(utc, ManilaTz);
                        return localPh.Date == date.Date;
                    })
                    .Select(x =>
                    {
                        var utc = NormalizeSupabaseUtc(x.AppointmentDateTime);
                        var localPh = TimeZoneInfo.ConvertTimeFromUtc(utc, ManilaTz);

                        var normalizedLocal = new DateTime(
                            localPh.Year, localPh.Month, localPh.Day, localPh.Hour, 0, 0);

                        return TimeZoneInfo.ConvertTimeToUtc(normalizedLocal, ManilaTz);
                    })
                    .ToList();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(ex);
                return new List<DateTime>();
            }
        }

        public async Task<bool> IsSlotAvailableAsync(DateTime utcTime, string? excludeSupabaseBookingId = null)
        {
            await EnsureInitializedAsync();
            var result = await _client!
                .From<SupabaseAppointmentEntry>()
                .Get();

            var targetLocal = TimeZoneInfo.ConvertTimeFromUtc(
                DateTime.SpecifyKind(utcTime, DateTimeKind.Utc), ManilaTz);

            return !result.Models.Any(a =>
            {
                if (a.Status == "cancelled" || a.Status == "completed" || a.Status == "rejected")
                    return false;

                if (excludeSupabaseBookingId != null &&
                    a.SupabaseBookingId == excludeSupabaseBookingId)
                    return false;

                var entryUtc = NormalizeSupabaseUtc(a.AppointmentDateTime);
                var dtLocal = TimeZoneInfo.ConvertTimeFromUtc(entryUtc, ManilaTz);

                return dtLocal.Year == targetLocal.Year &&
                       dtLocal.Month == targetLocal.Month &&
                       dtLocal.Day == targetLocal.Day &&
                       dtLocal.Hour == targetLocal.Hour;
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
                    .Where(x => x.SupabaseBookingId == appointmentEntryId)
                    .Single();

                if (result == null)
                {
                    System.Diagnostics.Debug.WriteLine(
                        $"[Supabase] Reschedule: no appointment found for SupabaseBookingId={appointmentEntryId}");
                    throw new InvalidOperationException("Appointment not found — it may have already been completed or removed.");
                }

                result.AppointmentDateTime = newUtcTime;
                // Status intentionally left unchanged — flipping it to "rescheduled"
                // can drop the appointment out of whatever status-filtered list/day
                // view was already displaying it. The new date/time is the only
                // thing that needs to change.

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

        // Looks up a patient by comparing the booking's full name against FirstName+LastName combined, whitespace/case normalized — avoids brittle first/last splitting mismatches.
        public async Task<SupabasePatient?> GetPatientByNameAsync(string fullName)
        {
            try
            {
                await EnsureInitializedAsync();

                var targetTokens = TokenizeName(fullName);
                if (targetTokens.Count == 0)
                    return null;

                var result = await _client!.From<SupabasePatient>().Get();
                var patients = result.Models ?? new List<SupabasePatient>();

                return patients.FirstOrDefault(p =>
                    NamesMatch(targetTokens, TokenizeName($"{p.FirstName} {p.LastName}")));
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[Supabase] GetPatientByName: {ex.Message}");
                return null;
            }
        }

        // Splits a name into lowercase word tokens, for tolerant comparisons.
        private static HashSet<string> TokenizeName(string name) =>
            (name ?? "").Split(' ', StringSplitOptions.RemoveEmptyEntries)
                .Select(w => w.Trim().ToLowerInvariant())
                .ToHashSet();

        // Matches names ignoring word order and tolerating extra words (e.g. a middle name) — the shorter name's words must all appear in the longer one.
        private static bool NamesMatch(HashSet<string> a, HashSet<string> b)
        {
            if (a.Count == 0 || b.Count == 0) return false;
            var shorter = a.Count <= b.Count ? a : b;
            var longer = a.Count <= b.Count ? b : a;

            // Guards against a single common word (e.g. "Maria") loosely matching any longer name that happens to contain it.
            if (shorter.Count < 2 && shorter.Count != longer.Count) return false;

            return shorter.IsSubsetOf(longer);
        }

        // Looks up a patient by phone, matching on the last 7 digits to tolerate formatting differences.
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

                var byId = bills.Where(b => b.PatientId == patientId).ToList();

                // Also match by name, so older bills created before proper ID-linking still show up
                // alongside correctly-linked ones. Isolated in its own try/catch — a bad patient row
                // (e.g. a null boolean column) shouldn't cost us the byId results we already have.
                var byName = new List<SupabaseBill>();
                try
                {
                    var patientResult = await _client!
                        .From<SupabasePatient>()
                        .Where(p => p.Id == patientId)
                        .Get();

                    var patient = patientResult.Models?.FirstOrDefault();
                    if (patient != null)
                    {
                        var fullName = $"{patient.FirstName} {patient.LastName}".Trim();
                        byName = bills
                            .Where(b => string.Equals(b.PatientName?.Trim(), fullName, StringComparison.OrdinalIgnoreCase))
                            .ToList();
                    }
                }
                catch (Exception nameLookupEx)
                {
                    System.Diagnostics.Debug.WriteLine($"[Supabase] GetBillsForPatient name-lookup skipped: {nameLookupEx.Message}");
                }

                return byId
                    .Concat(byName)
                    .GroupBy(b => b.Id)
                    .Select(g => g.First())
                    .OrderByDescending(b => b.VisitDate)
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

        // Computes what's actually due THIS visit, freshly, from the current
        // state of each bill_item — unlike Bill.MinimumDueToday, which is a
        // snapshot taken once at bill creation and never updated. Call this
        // every time the Payment page loads, not just on the first visit.
        // Calculates, purely from an item's stored fields, what it currently
        // owes according to its fixed schedule — and lets advance payments
        // net out naturally, since AmountPaid already reflects them.
        //
        // - "monthsElapsed" = how many monthly due dates have already
        //   passed, counting from InstallmentStartDate (the downpayment
        //   date), capped at InstallmentMonths.
        // - "totalOwedByNow" = what should have been paid by THIS point in
        //   the schedule if nothing had been paid early: downpayment, plus
        //   one MonthlyPayment for every month that's passed.
        // - AmountDueNow = totalOwedByNow minus whatever's actually been
        //   paid (AmountPaid) — so if the patient already paid ahead, this
        //   comes out to 0 (or less than a full MonthlyPayment) automatically,
        //   without needing to track "advance credit" as a separate number.
        //
        // Once every scheduled month has passed (monthsElapsed >=
        // InstallmentMonths), the schedule is over — whatever's left is
        // simply owed in full, same as a non-installment item.
        private static (decimal AmountDueNow, DateTime? NextDueDate) GetInstallmentDueState(
            SupabaseBillItem item)
        {
            if (!item.IsInstallment || item.Balance <= 0)
                return (0, null);

            // Downpayment not made yet — handled separately by the caller,
            // not via this schedule math (there's no start date to anchor on).
            if (item.AmountPaid <= 0 || !item.InstallmentStartDate.HasValue)
                return (0, null);

            var start = item.InstallmentStartDate.Value;
            var monthsElapsed = 0;

            while (monthsElapsed < item.InstallmentMonths &&
                   start.AddMonths(monthsElapsed + 1) <= DateTime.UtcNow)
            {
                monthsElapsed++;
            }

            if (monthsElapsed >= item.InstallmentMonths)
            {
                // Full term has elapsed by the calendar — no more "next
                // monthly due", whatever remains is simply owed in full.
                return (item.Balance, null);
            }

            var totalOwedByNow = Math.Min(
                item.DownpaymentAmount + (item.MonthlyPayment * monthsElapsed),
                item.Subtotal);

            var amountDueNow = Math.Max(0m, Math.Min(totalOwedByNow - item.AmountPaid, item.Balance));
            var nextDueDate = start.AddMonths(monthsElapsed + 1);

            return (amountDueNow, nextDueDate);
        }

        public async Task<decimal> GetMinimumDueForBillAsync(string billId)
        {
            try
            {
                var items = await GetBillItemsAsync(billId);
                decimal minimum = 0;

                foreach (var item in items)
                {
                    if (item.Balance <= 0)
                    {
                        System.Diagnostics.Debug.WriteLine(
                            $"[DIAG-MINIMUM] {item.ServiceName}: skipped (Balance={item.Balance} <= 0)");
                        continue;
                    }

                    if (!item.IsInstallment)
                    {
                        // Non-installment items are always due in full.
                        minimum += item.Balance;
                        System.Diagnostics.Debug.WriteLine(
                            $"[DIAG-MINIMUM] {item.ServiceName}: non-installment, contributes full Balance={item.Balance}");
                    }
                    else if (item.AmountPaid <= 0)
                    {
                        // Nothing paid on this item yet — the downpayment
                        // is what starts the schedule, still required.
                        var contribution = Math.Min(item.DownpaymentAmount, item.Balance);
                        minimum += contribution;
                        System.Diagnostics.Debug.WriteLine(
                            $"[DIAG-MINIMUM] {item.ServiceName}: AmountPaid=0, " +
                            $"DownpaymentAmount={item.DownpaymentAmount}, Balance={item.Balance}, " +
                            $"contributes={contribution}");
                    }
                    else
                    {
                        // Downpayment already made — no forced minimum unless
                        // a scheduled monthly due date has actually arrived,
                        // and any advance/early payment already made nets
                        // straight out of what's owed for that cycle.
                        var (amountDueNow, nextDueDate) = GetInstallmentDueState(item);
                        minimum += amountDueNow;
                        System.Diagnostics.Debug.WriteLine(
                            $"[DIAG-MINIMUM] {item.ServiceName}: AmountPaid={item.AmountPaid}, " +
                            $"nextDueDate={(nextDueDate.HasValue ? nextDueDate.Value.ToString("o") : "schedule complete")}, " +
                            $"contributes={amountDueNow}");
                    }
                }

                System.Diagnostics.Debug.WriteLine($"[DIAG-MINIMUM] TOTAL minimum={minimum}");

                return minimum;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[Supabase] GetMinimumDueForBill: {ex.Message}");
                return 0;
            }
        }

        // Distributes a payment across this bill's items instead of just the
        // bill-level total: non-installment items are paid off first (always
        // due in full), then installment items in due-date order (items that
        // haven't started their schedule yet — DueDate null — go first,
        // since that represents the still-unpaid downpayment stage). Each
        // item's own balance/amount_paid/due_date gets updated so the NEXT
        // visit's minimum (see GetMinimumDueForBillAsync above) reflects
        // what's actually still owed on THAT item, not a stale bill-wide figure.
        //
        // FIX: this used to catch-and-log any failure here and carry on —
        // meaning a payment could "succeed" at the bill level while every
        // bill_item stayed completely untouched (AmountPaid/Balance never
        // moved), which is exactly what silently broke "due today" on repeat
        // visits. Now returns success/error so the caller can stop BEFORE
        // recording anything else, instead of leaving bill-level and
        // item-level data out of sync.
        private async Task<(bool Success, string? Error)> AllocatePaymentToBillItemsAsync(
            string billId, decimal amount, DateTime paymentDate)
        {
            try
            {
                var items = await GetBillItemsAsync(billId);
                var remaining = amount;

                var ordered = items
                    .Where(i => i.Balance > 0)
                    .OrderBy(i => i.IsInstallment ? 1 : 0)
                    .ThenBy(i => i.DueDate ?? DateTime.MinValue)
                    .ToList();

                foreach (var item in ordered)
                {
                    if (remaining <= 0) break;

                    var applied = Math.Min(item.Balance, remaining);
                    var wasUnstarted = item.IsInstallment && item.AmountPaid <= 0;

                    item.AmountPaid += applied;
                    item.Balance -= applied;
                    item.LastPaymentDate = paymentDate;

                    // Anchor the schedule ONCE, right when the downpayment
                    // lands — never touched again after this, so a later
                    // early/advance payment can't shift it.
                    if (wasUnstarted && item.InstallmentStartDate == null)
                        item.InstallmentStartDate = paymentDate;

                    // DueDate is stored purely for display (Bill Details,
                    // Receipt overdue flags) — always recomputed from the
                    // fixed schedule, never bumped by "1 month from whenever
                    // this payment happened" like before.
                    item.DueDate = item.Balance <= 0
                        ? null
                        : (item.IsInstallment
                            ? GetInstallmentDueState(item).NextDueDate
                            : item.DueDate);

                    var updateResult = await _client!.From<SupabaseBillItem>().Update(item);

                    System.Diagnostics.Debug.WriteLine(
                        $"[DIAG-ALLOCATE] {item.ServiceName}: applied={applied} " +
                        $"newBalance={item.Balance} IsInstallment={item.IsInstallment} " +
                        $"computedDueDate={(item.DueDate.HasValue ? item.DueDate.Value.ToString("o") : "NULL")} " +
                        $"Update() returned {updateResult.Models.Count} row(s).");

                    // A 0-row response means the update didn't actually
                    // touch anything in the DB (most commonly an RLS policy
                    // silently blocking the write) — treat that the same as
                    // a thrown exception, not a success.
                    if (updateResult.Models.Count == 0)
                    {
                        return (false,
                            $"Update to bill_items for '{item.ServiceName}' affected 0 rows " +
                            "— check Supabase RLS policies allow UPDATE on bill_items for this user/role.");
                    }

                    remaining -= applied;
                }

                return (true, null);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[Supabase] AllocatePaymentToBillItems: {ex.Message}");
                return (false, ex.Message);
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

                var paymentDate = DateTime.UtcNow;

                // Allocate to bill_items FIRST, before writing anything else.
                // If this fails, nothing else has been touched yet, so there's
                // nothing to roll back — the caller gets a clear error instead
                // of a payment that "succeeded" while items stayed stale.
                var (allocated, allocError) =
                    await AllocatePaymentToBillItemsAsync(billId, amount, paymentDate);

                if (!allocated)
                {
                    return (false,
                        $"Payment was not recorded — could not update bill items ({allocError}).");
                }

                var payment = new SupabasePayment
                {
                    Id = Guid.NewGuid().ToString(),
                    BillId = billId,
                    Amount = amount,
                    PaymentDate = paymentDate,
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
                    {
                        // Bill-level DueDate is just a display rollup — mirror
                        // whatever the item-level schedule (already recomputed
                        // and anchored in AllocatePaymentToBillItemsAsync above)
                        // actually says is next, instead of a flat "+1 month
                        // from today", which used to push the due date forward
                        // on every advance/early payment even though the real,
                        // anchored per-item schedule hadn't moved at all.
                        var refreshedItems = await GetBillItemsAsync(billId);
                        billResult.DueDate = refreshedItems
                            .Where(i => i.Balance > 0 && i.DueDate.HasValue)
                            .OrderBy(i => i.DueDate)
                            .Select(i => (DateTime?)i.DueDate)
                            .FirstOrDefault();
                    }
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

        // ── Treatment History ─────────────────────────────────────────

        public async Task<SupabaseTreatmentHistory?> AddTreatmentHistoryAsync(
            SupabaseTreatmentHistory entry)
        {
            try
            {
                await EnsureInitializedAsync();
                var result = await _client!
                    .From<SupabaseTreatmentHistory>()
                    .Insert(entry);
                return result.Models.FirstOrDefault();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(
                    $"[Supabase] AddTreatmentHistory: {ex.Message}");
                return null;
            }
        }

        public async Task<List<SupabaseTreatmentHistory>> GetTreatmentHistoryAsync(
            string patientId)
        {
            try
            {
                await EnsureInitializedAsync();
                var result = await _client!
                    .From<SupabaseTreatmentHistory>()
                    .Where(h => h.PatientId == patientId)
                    .Get();
                return result.Models ?? new List<SupabaseTreatmentHistory>();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(
                    $"[Supabase] GetTreatmentHistory: {ex.Message}");
                return new List<SupabaseTreatmentHistory>();
            }
        }

        // ── Tooth Records ─────────────────────────────────────────────

        public async Task<SupabaseToothRecord?> UpsertToothRecordAsync(SupabaseToothRecord record)
        {
            try
            {
                await EnsureInitializedAsync();
                var result = await _client!
                    .From<SupabaseToothRecord>()
                    .OnConflict("patient_id,tooth_number")   // string overload, snake_case DB names
                    .Upsert(record);
                return result.Models.FirstOrDefault();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[Supabase] UpsertToothRecord: {ex.Message}");
                return null;
            }
        }

        public async Task<List<SupabaseToothRecord>> GetToothRecordsAsync(string patientId)
        {
            try
            {
                await EnsureInitializedAsync();
                var result = await _client!
                    .From<SupabaseToothRecord>()
                    .Where(r => r.PatientId == patientId)
                    .Get();
                return result.Models ?? new List<SupabaseToothRecord>();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[Supabase] GetToothRecords: {ex.Message}");
                return new List<SupabaseToothRecord>();
            }
        }

        // Reads supply_stock_logs for the Reports page's Medical Supply
        // chart — every restock/usage/adjustment is already logged here
        // by ApplyStockChangeAsync above, so this just filters that
        // existing log to the selected period (same fetch-then-filter
        // pattern as GetAllBookingsForReportAsync below).
        public async Task<List<SupabaseStockLog>> GetAllStockLogsForReportAsync(
            DateTime rangeStart, DateTime rangeEnd)
        {
            try
            {
                await EnsureInitializedAsync();
                var result = await _client!.From<SupabaseStockLog>().Get();

                return result.Models
                    .Where(l => l.CreatedAt >= rangeStart && l.CreatedAt < rangeEnd)
                    .OrderBy(l => l.CreatedAt)
                    .ToList();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[Supabase] GetAllStockLogsForReport: {ex.Message}");
                return new List<SupabaseStockLog>();
            }
        }

        // ── Reports support ────────────────────────────────────────────

        public async Task<List<SupabaseBooking>> GetAllBookingsForReportAsync(
            DateTime rangeStart, DateTime rangeEnd)
        {
            try
            {
                await EnsureInitializedAsync();
                var result = await _client!.From<SupabaseBooking>().Get();

                return result.Models
                    .Where(b => b.AppointmentDate >= rangeStart && b.AppointmentDate < rangeEnd)
                    .OrderBy(b => b.AppointmentDate)
                    .ToList();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[Supabase] GetAllBookingsForReport: {ex.Message}");
                return new List<SupabaseBooking>();
            }
        }

        // Gets every appointment_entries row (the real schedule) within a date range, for the Reports page.
        public async Task<List<SupabaseAppointmentEntry>> GetAllAppointmentEntriesForReportAsync(
            DateTime rangeStart, DateTime rangeEnd)
        {
            try
            {
                await EnsureInitializedAsync();
                var result = await _client!.From<SupabaseAppointmentEntry>().Get();

                return result.Models
                    .Where(a => a.AppointmentDateTime >= rangeStart && a.AppointmentDateTime < rangeEnd)
                    .OrderBy(a => a.AppointmentDateTime)
                    .ToList();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[Supabase] GetAllAppointmentEntriesForReport: {ex.Message}");
                return new List<SupabaseAppointmentEntry>();
            }
        }

        // Writes one row to cancelled_appointments — called right before an appointment_entries row gets deleted, so Reports still has something to count later.
        public async Task LogCancelledAppointmentAsync(DateTime originalAppointmentDateTime, string? patientName)
        {
            try
            {
                await EnsureInitializedAsync();
                var log = new SupabaseCancelledAppointment
                {
                    Id = Guid.NewGuid().ToString(),
                    AppointmentDateTime = originalAppointmentDateTime,
                    PatientName = patientName,
                    CancelledAt = DateTime.UtcNow
                };
                await _client!.From<SupabaseCancelledAppointment>().Insert(log);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[Supabase] LogCancelledAppointment: {ex.Message}");
            }
        }

        // Gets every logged cancellation whose ORIGINAL appointment date falls within a date range, for the Reports page.
        public async Task<List<SupabaseCancelledAppointment>> GetAllCancelledAppointmentsForReportAsync(
            DateTime rangeStart, DateTime rangeEnd)
        {
            try
            {
                await EnsureInitializedAsync();
                var result = await _client!.From<SupabaseCancelledAppointment>().Get();

                return result.Models
                    .Where(c => c.AppointmentDateTime >= rangeStart && c.AppointmentDateTime < rangeEnd)
                    .OrderBy(c => c.AppointmentDateTime)
                    .ToList();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[Supabase] GetAllCancelledAppointmentsForReport: {ex.Message}");
                return new List<SupabaseCancelledAppointment>();
            }
        }

        public async Task<List<SupabaseTreatmentHistory>> GetAllTreatmentHistoryForReportAsync(
    DateTime rangeStart, DateTime rangeEnd)
        {
            try
            {
                await EnsureInitializedAsync();
                var result = await _client!.From<SupabaseTreatmentHistory>().Get();

                return result.Models
                    .Where(h => h.CreatedAt.HasValue
                             && h.CreatedAt.Value >= rangeStart
                             && h.CreatedAt.Value < rangeEnd)
                    .ToList();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[Supabase] GetAllTreatmentHistoryForReport: {ex.Message}");
                return new List<SupabaseTreatmentHistory>();
            }
        }

        public async Task<List<SupabaseBillItem>> GetAllBillItemsAsync()
        {
            try
            {
                await EnsureInitializedAsync();
                var result = await _client!.From<SupabaseBillItem>().Get();
                return result.Models ?? new List<SupabaseBillItem>();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[Supabase] GetAllBillItems: {ex.Message}");
                return new List<SupabaseBillItem>();
            }
        }

        // ── Treatment Sequences ───────────────────────────────────────

        public async Task<SupabaseService?> GetServiceByIdAsync(string serviceId)
        {
            try
            {
                await EnsureInitializedAsync();
                return await _client!
                    .From<SupabaseService>()
                    .Where(s => s.Id == serviceId)
                    .Single();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[Supabase] GetServiceById: {ex.Message}");
                return null;
            }
        }

        public async Task<List<SupabaseTreatmentSequence>> GetTreatmentSequencesForPatientAsync(string patientId)
        {
            try
            {
                await EnsureInitializedAsync();
                var result = await _client!
                    .From<SupabaseTreatmentSequence>()
                    .Where(t => t.PatientId == patientId)
                    .Order("created_at", Supabase.Postgrest.Constants.Ordering.Ascending)
                    .Get();
                return result.Models ?? new List<SupabaseTreatmentSequence>();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[Supabase] GetTreatmentSequencesForPatient: {ex.Message}");
                return new List<SupabaseTreatmentSequence>();
            }
        }

        /// Most recent open (awaiting_schedule/scheduled) sequence for this patient+service,
        /// or the latest completed one if the treatment has no open row.
        public async Task<SupabaseTreatmentSequence?> GetActiveSequenceAsync(string patientId, string serviceId)
        {
            try
            {
                await EnsureInitializedAsync();
                var result = await _client!
                    .From<SupabaseTreatmentSequence>()
                    .Where(t => t.PatientId == patientId && t.ServiceId == serviceId)
                    .Order("session_number", Supabase.Postgrest.Constants.Ordering.Descending)
                    .Get();

                var all = result.Models ?? new List<SupabaseTreatmentSequence>();
                return all.FirstOrDefault(t => t.Status != "completed") ?? all.FirstOrDefault();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[Supabase] GetActiveSequence: {ex.Message}");
                return null;
            }
        }

        /// Records a just-billed session for a multi-session service. Advances the session
        /// number from any prior row for this patient+service (starts at 1 if none exists).
        /// Returns null if a duplicate would be created (an awaiting_schedule/scheduled row
        /// for this patient+service already exists — i.e. staff hasn't done the next visit yet).
        public async Task<SupabaseTreatmentSequence?> RecordCompletedSessionAsync(
            string patientId, string patientName, SupabaseService service, string? sourceAppointmentId)
        {
            try
            {
                await EnsureInitializedAsync();

                var previous = await GetActiveSequenceAsync(patientId, service.Id);

                // Guard against double-recording the same session (e.g. a bill retried)
                if (previous != null && previous.Status != "completed")
                {
                    System.Diagnostics.Debug.WriteLine(
                        $"[Supabase] RecordCompletedSession: open sequence already exists for {service.Name}, skipping duplicate.");
                    return null;
                }

                int nextSessionNumber = (previous?.SessionNumber ?? 0) + 1;
                int totalSessions = previous?.TotalSessions ?? service.DefaultTotalSessions ?? 1;
                bool isFinal = nextSessionNumber >= totalSessions;

                var row = new SupabaseTreatmentSequence
                {
                    Id = Guid.NewGuid().ToString(),
                    PatientId = patientId,
                    PatientName = patientName,
                    ServiceId = service.Id,
                    ServiceName = service.Name,
                    SessionNumber = nextSessionNumber,
                    TotalSessions = totalSessions,
                    SourceAppointmentId = sourceAppointmentId,
                    Status = isFinal ? "completed" : "awaiting_schedule",
                    RecommendedDate = (!isFinal && service.FollowupIntervalDays.HasValue)
                        ? DateTime.UtcNow.AddDays(service.FollowupIntervalDays.Value)
                        : null,
                    CreatedAt = DateTime.UtcNow
                };

                var result = await _client!.From<SupabaseTreatmentSequence>().Insert(row);
                return result.Models.FirstOrDefault();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[Supabase] RecordCompletedSession: {ex.Message}");
                return null;
            }
        }

        /// Builds what the next session row would look like WITHOUT writing anything to Supabase —
        /// used by BillSummaryViewModel before payment, so the dentist can review/schedule the
        /// follow-up before anything is committed. Call PersistSessionAsync with the returned row
        /// once payment actually succeeds.
        public async Task<SupabaseTreatmentSequence?> PreviewNextSessionAsync(
            string patientId, string patientName, SupabaseService service, string? sourceAppointmentId)
        {
            try
            {
                await EnsureInitializedAsync();

                var previous = await GetActiveSequenceAsync(patientId, service.Id);

                // Guard against double-recording the same session (e.g. a bill retried)
                if (previous != null && previous.Status != "completed")
                {
                    System.Diagnostics.Debug.WriteLine(
                        $"[Supabase] PreviewNextSession: open sequence already exists for {service.Name}, skipping duplicate.");
                    return null;
                }

                int nextSessionNumber = (previous?.SessionNumber ?? 0) + 1;
                int totalSessions = previous?.TotalSessions ?? service.DefaultTotalSessions ?? 1;
                bool isFinal = nextSessionNumber >= totalSessions;

                return new SupabaseTreatmentSequence
                {
                    Id = Guid.NewGuid().ToString(),
                    PatientId = patientId,
                    PatientName = patientName,
                    ServiceId = service.Id,
                    ServiceName = service.Name,
                    SessionNumber = nextSessionNumber,
                    TotalSessions = totalSessions,
                    SourceAppointmentId = sourceAppointmentId,
                    Status = isFinal ? "completed" : "awaiting_schedule",
                    RecommendedDate = (!isFinal && service.FollowupIntervalDays.HasValue)
                        ? DateTime.UtcNow.AddDays(service.FollowupIntervalDays.Value)
                        : null,
                    CreatedAt = DateTime.UtcNow
                };
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[Supabase] PreviewNextSession: {ex.Message}");
                return null;
            }
        }

        /// Actually inserts a previously-previewed session row (see PreviewNextSessionAsync above) —
        /// call only once payment has succeeded, so a bill that never completes never creates a row.
        public async Task<SupabaseTreatmentSequence?> PersistSessionAsync(SupabaseTreatmentSequence row)
        {
            try
            {
                await EnsureInitializedAsync();
                var result = await _client!.From<SupabaseTreatmentSequence>().Insert(row);
                return result.Models.FirstOrDefault();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[Supabase] PersistSession: {ex.Message}");
                return null;
            }
        }

        /// Links a newly booked follow-up appointment back to its treatment sequence row.
        public async Task LinkNextAppointmentToSequenceAsync(string sequenceId, string nextAppointmentCorrelationId)
        {
            try
            {
                await EnsureInitializedAsync();
                var row = await _client!
                    .From<SupabaseTreatmentSequence>()
                    .Where(t => t.Id == sequenceId)
                    .Single();
                if (row == null) return;

                row.NextAppointmentId = nextAppointmentCorrelationId;
                row.Status = "scheduled";
                await _client!.From<SupabaseTreatmentSequence>().Update(row);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[Supabase] LinkNextAppointmentToSequence: {ex.Message}");
            }
        }

        /// Every sequence row still waiting to be scheduled — feeds the schedule page's banner.
        public async Task<List<SupabaseTreatmentSequence>> GetPendingFollowUpsAsync()
        {
            try
            {
                await EnsureInitializedAsync();
                var result = await _client!
                    .From<SupabaseTreatmentSequence>()
                    .Where(t => t.Status == "awaiting_schedule")
                    .Order("recommended_date", Supabase.Postgrest.Constants.Ordering.Ascending)
                    .Get();
                return result.Models ?? new List<SupabaseTreatmentSequence>();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[Supabase] GetPendingFollowUps: {ex.Message}");
                return new List<SupabaseTreatmentSequence>();
            }
        }

        /// Creates the follow-up AppointmentEntry (local + Supabase), tags it as the
        /// continued session, links it back to the sequence, and syncs a Google Task —
        /// the single place both the bill-completion sheet and the pending-follow-ups
        /// list call into, so there's one creation path instead of a dedicated page.
        public async Task<bool> CreateFollowUpAppointmentAsync(
    DatabaseService db,
    SupabaseTreatmentSequence sequence,
    string phone,
    string email,
    DateTime localAppointmentDateTime,
    DateTime utcAppointmentDateTime)
        {
            try
            {
                var available = await IsSlotAvailableAsync(utcAppointmentDateTime);
                if (!available) return false;

                var correlationId = Guid.NewGuid().ToString();
                var nextSessionNumber = sequence.SessionNumber + 1;
                var noteText = $"Follow-up: {sequence.ServiceName} — Session {nextSessionNumber} of {sequence.TotalSessions}";

                var localEntry = new AppointmentEntry
                {
                    SupabaseBookingId = correlationId,
                    PatientName = sequence.PatientName,
                    PatientSupabaseId = sequence.PatientId,
                    Phone = phone,
                    Email = email,
                    Notes = noteText,
                    AppointmentDateTime = localAppointmentDateTime.ToString("yyyy-MM-dd HH:mm:ss"),
                    Status = "approved",
                    TreatmentSequenceId = sequence.Id,
                    SessionNumber = nextSessionNumber,
                    TotalSessions = sequence.TotalSessions
                };
                await db.AddAppointmentEntry(localEntry);

                var supEntry = new SupabaseAppointmentEntry
                {
                    SupabaseBookingId = correlationId,
                    PatientName = sequence.PatientName,
                    PatientId = sequence.PatientId,
                    Phone = phone,
                    Email = email,
                    Notes = noteText,
                    AppointmentDateTime = utcAppointmentDateTime,
                    Status = "approved",
                    TreatmentSequenceId = sequence.Id,
                    SessionNumber = nextSessionNumber,
                    TotalSessions = sequence.TotalSessions
                };

                var created = await AddAppointmentEntryAsync(supEntry);
                if (created == null) return false;

                await LinkNextAppointmentToSequenceAsync(sequence.Id, correlationId);

                try
                {
                    await SyncToGoogleTasksAsync(
                        "", sequence.PatientName, noteText, localAppointmentDateTime, phone, noteText);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[Supabase] CreateFollowUpAppointment GoogleTasks: {ex.Message}");
                }

                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[Supabase] CreateFollowUpAppointment: {ex.Message}");
                return false;
            }
        }
        public async Task<List<SupabaseTreatmentSequence>> GetScheduledFollowUpsAsync()
        {
            try
            {
                await EnsureInitializedAsync();
                var result = await _client!
                    .From<SupabaseTreatmentSequence>()
                    .Where(t => t.Status == "scheduled")
                    .Order("recommended_date", Supabase.Postgrest.Constants.Ordering.Ascending)
                    .Get();
                return result.Models ?? new List<SupabaseTreatmentSequence>();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[Supabase] GetScheduledFollowUps: {ex.Message}");
                return new List<SupabaseTreatmentSequence>();
            }
        }

        public async Task<SupabaseAppointmentEntry?> GetAppointmentEntryByBookingIdAsync(string supabaseBookingId)
        {
            try
            {
                await EnsureInitializedAsync();
                return await _client!
                    .From<SupabaseAppointmentEntry>()
                    .Where(x => x.SupabaseBookingId == supabaseBookingId)
                    .Single();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[Supabase] GetAppointmentEntryByBookingId: {ex.Message}");
                return null;
            }
        }

        /// Correctly matches by supabase_booking_id (the correlation id this app actually
        /// controls at creation time) rather than the row's DB-generated id.
        public async Task<bool> UpdateAppointmentEntryDateTimeAsync(string supabaseBookingId, DateTime newUtcTime)
        {
            try
            {
                await EnsureInitializedAsync();
                var entry = await _client!
                    .From<SupabaseAppointmentEntry>()
                    .Where(x => x.SupabaseBookingId == supabaseBookingId)
                    .Single();
                if (entry == null) return false;

                entry.AppointmentDateTime = newUtcTime;
                entry.Status = "rescheduled";
                await _client!.From<SupabaseAppointmentEntry>().Update(entry);
                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[Supabase] UpdateAppointmentEntryDateTime: {ex.Message}");
                return false;
            }
        }

        // ── Activity Log ──────────────────────────────────────────

        // Writes one activity row. Call this from wherever the actual action happens (payment recorded, patient added, etc.).
        public async Task LogActivityAsync(string type, string description, string? relatedId = null)
        {
            try
            {
                await EnsureInitializedAsync();
                await _client!.From<SupabaseActivityLog>().Insert(new SupabaseActivityLog
                {
                    Type = type,
                    Description = description,
                    RelatedId = relatedId,
                    CreatedAt = DateTime.UtcNow
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[Supabase] LogActivity: {ex.Message}");
            }
        }

        // Newest activities first, capped to count — used by Home's Recent Activity card.
        public async Task<List<SupabaseActivityLog>> GetRecentActivitiesAsync(int count = 10)
        {
            try
            {
                await EnsureInitializedAsync();
                var result = await _client!
                    .From<SupabaseActivityLog>()
                    .Order("created_at", Supabase.Postgrest.Constants.Ordering.Descending)
                    .Limit(count)
                    .Get();
                return result.Models ?? new List<SupabaseActivityLog>();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[Supabase] GetRecentActivities: {ex.Message}");
                return new List<SupabaseActivityLog>();
            }
        }

        // Full activity history, newest first — used by the "View All" page.
        public async Task<List<SupabaseActivityLog>> GetAllActivitiesAsync()
        {
            try
            {
                await EnsureInitializedAsync();
                var result = await _client!
                    .From<SupabaseActivityLog>()
                    .Order("created_at", Supabase.Postgrest.Constants.Ordering.Descending)
                    .Get();
                return result.Models ?? new List<SupabaseActivityLog>();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[Supabase] GetAllActivities: {ex.Message}");
                return new List<SupabaseActivityLog>();
            }
        }
    }
}
