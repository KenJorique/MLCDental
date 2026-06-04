using ClinicApp.Models;
using Supabase.Realtime;
using Supabase.Realtime.PostgresChanges;
using static Supabase.Realtime.PostgresChanges.PostgresChangesOptions;

namespace ClinicApp.Services
{
    public class SupabaseRealtimeService
    {
        private Supabase.Client? _client;
        private readonly DatabaseService _db;
        private bool _initialized = false;

        public event Action? OnNewBookingReceived;
        public event Action? OnPatientChanged;  // ← new event

        public SupabaseRealtimeService(DatabaseService db)
        {
            _db = db;
        }

        public async Task InitializeAsync(string url, string key)
        {
            if (_initialized) return;
            var options = new Supabase.SupabaseOptions
            {
                AutoConnectRealtime = true
            };
            _client = new Supabase.Client(url, key, options);
            await _client.InitializeAsync();
            _initialized = true;
        }

        public async Task SubscribeToBookingsAsync()
        {
            if (_client == null) return;
            try
            {
                var channel = _client.Realtime.Channel("realtime-bookings");
                channel.Register(new PostgresChangesOptions("public", "bookings"));
                channel.AddPostgresChangeHandler(ListenType.Inserts, async (sender, change) =>
                {
                    try
                    {
                        var booking = change.Model<SupabaseBooking>();
                        if (booking == null) return;
                        await _db.AddPendingAppointment(new Appointment
                        {
                            SupabaseBookingId = booking.Id,
                            FullName = booking.FullName ?? "",
                            Phone = booking.Phone ?? "",
                            Email = booking.Email ?? "",
                            Service = booking.Service ?? "",
                            AppointmentDate = booking.AppointmentDate.ToString("yyyy-MM-dd HH:mm:ss"),
                            Notes = booking.Notes ?? "",
                            Status = "pending"
                        });
                        MainThread.BeginInvokeOnMainThread(() => OnNewBookingReceived?.Invoke());
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"[Realtime] Booking insert error: {ex.Message}");
                    }
                });
                await channel.Subscribe();
                System.Diagnostics.Debug.WriteLine("[Realtime] Subscribed to bookings.");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[Realtime] SubscribeBookings error: {ex.Message}");
            }
        }

        // ← New: subscribe to patient inserts AND updates
        public async Task SubscribeToPatientsAsync()
        {
            if (_client == null) return;
            try
            {
                var channel = _client.Realtime.Channel("realtime-patients");
                channel.Register(new PostgresChangesOptions("public", "patients"));

                // New patient added on another device → sync to local SQLite
                channel.AddPostgresChangeHandler(ListenType.Inserts, async (sender, change) =>
                {
                    try
                    {
                        var sp = change.Model<SupabasePatient>();
                        if (sp == null) return;
                        System.Diagnostics.Debug.WriteLine(
                            $"[Realtime] New patient from another device: {sp.FirstName}");
                        await _db.SyncPatientFromSupabase(sp);
                        MainThread.BeginInvokeOnMainThread(() => OnPatientChanged?.Invoke());
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"[Realtime] Patient insert error: {ex.Message}");
                    }
                });

                // Patient edited on another device → update local SQLite
                channel.AddPostgresChangeHandler(ListenType.Updates, async (sender, change) =>
                {
                    try
                    {
                        var sp = change.Model<SupabasePatient>();
                        if (sp == null) return;
                        System.Diagnostics.Debug.WriteLine(
                            $"[Realtime] Patient updated from another device: {sp.FirstName}");
                        await _db.SyncPatientFromSupabase(sp);
                        MainThread.BeginInvokeOnMainThread(() => OnPatientChanged?.Invoke());
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"[Realtime] Patient update error: {ex.Message}");
                    }
                });

                await channel.Subscribe();
                System.Diagnostics.Debug.WriteLine("[Realtime] Subscribed to patients.");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[Realtime] SubscribePatients error: {ex.Message}");
            }
        }

        public async Task SyncMissedBookingsAsync()
        {
            if (_client == null) return;
            try
            {
                var result = await _client.From<SupabaseBooking>()
                    .Where(b => b.Status == "pending").Get();
                foreach (var booking in result.Models)
                    await _db.AddPendingAppointment(new Appointment
                    {
                        SupabaseBookingId = booking.Id,
                        FullName = booking.FullName ?? "",
                        Phone = booking.Phone ?? "",
                        Email = booking.Email ?? "",
                        Service = booking.Service ?? "",
                        AppointmentDate = booking.AppointmentDate.ToString("yyyy-MM-dd HH:mm:ss"),
                        Notes = booking.Notes ?? "",
                        Status = "pending"
                    });
                System.Diagnostics.Debug.WriteLine(
                    $"[Sync] Missed bookings: {result.Models.Count}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[Sync] SyncMissedBookings error: {ex.Message}");
            }
        }

        // Pull all approved patients from Supabase and sync to local SQLite
        public async Task SyncMissedPatientsAsync()
        {
            if (_client == null) return;
            try
            {
                var result = await _client.From<SupabasePatient>().Get();
                int count = 0;
                foreach (var sp in result.Models)
                {
                    await _db.SyncPatientFromSupabase(sp);
                    count++;
                }
                System.Diagnostics.Debug.WriteLine($"[Sync] Missed patients synced: {count}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[Sync] SyncMissedPatients error: {ex.Message}");
            }
        }
    }
}