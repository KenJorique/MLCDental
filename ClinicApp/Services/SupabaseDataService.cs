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
    }
}