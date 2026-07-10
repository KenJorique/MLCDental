using ClinicApp.Models;
using SQLite;

namespace ClinicApp.Services;

public class DatabaseService
{
    // SQLite async connection, initialized once via Init()
    SQLiteAsyncConnection? _database;

    public async Task Init()
    {
        // Already fully initialised — skip
        if (_database != null) return;

        try
        {
            // this saves in windows
            string dbPath = Path.Combine(FileSystem.AppDataDirectory, "clinic.db3");
            System.Diagnostics.Debug.WriteLine($"[DB] Path: {dbPath}");

            //string dbPath = Path.Combine(FileSystem.AppDataDirectory, "clinic.db3");
            
            // This saves it to the "Downloads" folder on the Android Emulator
            string dbPath = Path.Combine("/storage/emulated/0/Download", "clinic.db3");
            System.Diagnostics.Debug.WriteLine($"[DB] Path: {dbPath}");
            // this saves in windows

            //MESSAGE FOR FINDING THE DATABASE PATH
            //  await Shell.Current.DisplayAlert(
            //"DB PATH",
            //dbPath,
            //"OK");

            _database = new SQLiteAsyncConnection(
                dbPath,
                SQLiteOpenFlags.ReadWrite | SQLiteOpenFlags.Create | SQLiteOpenFlags.SharedCache);

            // Clear synced booking cache so missed bookings get re-synced
            try
            {
                await _database!.ExecuteAsync("DELETE FROM SyncedBooking");
                System.Diagnostics.Debug.WriteLine("[DB] Cleared SyncedBooking cache");
            }
            catch { }

            // Run each pragma and table creation individually with its own try/catch
            try { await _database.ExecuteAsync("PRAGMA journal_mode=WAL;"); }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[DB] WAL pragma: {ex.Message}"); }

            try { await _database.ExecuteAsync("PRAGMA busy_timeout=3000;"); }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[DB] busy_timeout: {ex.Message}"); }

            try { await _database!.CreateTableAsync<Patient>(); }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[DB] Patient table: {ex.Message}"); }

            // Inside Init(), after CreateTableAsync<Patient>()
            try
            {
                await _database!.ExecuteAsync("ALTER TABLE Patient ADD COLUMN SupabaseId TEXT DEFAULT ''");
                System.Diagnostics.Debug.WriteLine("[DB] SupabaseId column added");
            }
            catch { /* already exists — ignore */ }


            try { await _database.CreateTableAsync<ServiceModel>(); }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[DB] ServiceModel table: {ex.Message}"); }
            try { await _database.ExecuteAsync("ALTER TABLE ServiceModel ADD COLUMN IsDeleted INTEGER DEFAULT 0"); } catch { }

            try { await _database.CreateTableAsync<User>(); }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[DB] User table: {ex.Message}"); }

            try { await _database.CreateTableAsync<ToothRecord>(); }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[DB] ToothRecord table: {ex.Message}"); }

            try { await _database.CreateTableAsync<CephalometricImage>(); }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[DB] CephalometricImage table: {ex.Message}"); }

            try { await _database.CreateTableAsync<TreatmentHistory>(); }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[DB] TreatmentHistory table: {ex.Message}"); }

            try { await _database.CreateTableAsync<SupplyStockLog>(); }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[DB] SupplyStockLog table: {ex.Message}"); }

            try { await _database.CreateTableAsync<SupplyItem>(); }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[DB] SupplyItem table: {ex.Message}"); }

            // Migrations — safe to run every time, SQLite ignores duplicate columns
            try { await _database.ExecuteAsync("ALTER TABLE SupplyItem ADD COLUMN Unit TEXT DEFAULT 'Per Piece'"); } catch { }
            try { await _database.ExecuteAsync("ALTER TABLE SupplyItem ADD COLUMN PiecesPerUnit INTEGER DEFAULT 1"); } catch { }
            try { await _database.ExecuteAsync("ALTER TABLE SupplyItem ADD COLUMN IsDeleted INTEGER DEFAULT 0"); } catch { }
            try { await _database.ExecuteAsync("ALTER TABLE User ADD COLUMN IsDeleted INTEGER DEFAULT 0"); } catch { }

            // User management migrations (contact, email, active status)
            try { await _database.ExecuteAsync("ALTER TABLE User ADD COLUMN ContactNo TEXT"); } catch { }
            try { await _database.ExecuteAsync("ALTER TABLE User ADD COLUMN Email TEXT"); } catch { }
            try { await _database.ExecuteAsync("ALTER TABLE User ADD COLUMN IsActive INTEGER DEFAULT 1"); } catch { }

            // Patient personal info last-updated timestamp
            try { await _database.ExecuteAsync("ALTER TABLE Patient ADD COLUMN LastUpdated TEXT DEFAULT ''"); } catch { }

            try { await _database.CreateTableAsync<Guardian>(); }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[DB] Guardian table: {ex.Message}"); }

            try { await _database.CreateTableAsync<MedicalHistory>(); }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[DB] MedicalHistory table: {ex.Message}"); }

            try { await _database.CreateTableAsync<Allergy>(); }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[DB] Allergy table: {ex.Message}"); }

            try { await _database.CreateTableAsync<MedicalCondition>(); }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[DB] MedicalCondition table: {ex.Message}"); }

            try { await _database.CreateTableAsync<PatientCondition>(); }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[DB] PatientCondition table: {ex.Message}"); }

            try { await _database.CreateTableAsync<SyncedBooking>(); }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[DB] SyncedBooking table: {ex.Message}"); }

            try { await _database.CreateTableAsync<Appointment>(); }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[DB] PendingAppointment table: {ex.Message}"); }

            try { await _database!.CreateTableAsync<AppointmentEntry>(); }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[DB] AppointmentEntry: {ex.Message}"); }

            try
            { await _database!.ExecuteAsync(
                    "ALTER TABLE AppointmentEntry ADD COLUMN GoogleTaskId TEXT DEFAULT ''"); }
            catch { /* already exists */ }

            System.Diagnostics.Debug.WriteLine("[DB] Init complete.");

            // Seed default users on first run
            try
            {
                var userCount = await _database.Table<User>().CountAsync();
                if (userCount == 0)
                {
                    await _database.InsertAllAsync(new List<User>
                    {
                        new User { FullName = "Dr. Full Name",  Username = "dentist1", Password = "123", Role = "Dentist",   IsActive = true },
                        new User { FullName = "Assistant Name", Username = "staff1",   Password = "123", Role = "Assistant", IsActive = true }
                    });
                    System.Diagnostics.Debug.WriteLine("[DB] Default users seeded.");
                }
            }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[DB] Seed error: {ex.Message}"); }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[DB] Connection error: {ex.Message}");
            _database = null;
        }
    }

    //for google tasks
    public async System.Threading.Tasks.Task UpdateAppointmentEntry(
    AppointmentEntry entry)
    {
        await Init();
        await _database!.UpdateAsync(entry);
    }

    // =========================
    // PATIENT CRUD
    // =========================

    public async Task<List<Patient>> GetPatients()
    {
        await Init();
        return await _database!.Table<Patient>().ToListAsync();
    }

    public async Task AddPatient(Patient patient)
    {
        try
        {
            await Init();
            int result = await _database!.InsertAsync(patient);
            System.Diagnostics.Debug.WriteLine($"Inserted: {result}");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine(ex.ToString());
        }
    }

    public async Task UpdatePatient(Patient patient)
    {
        await Init();
        await _database!.UpdateAsync(patient);
    }

    public async Task DeletePatient(Patient patient)
    {
        await Init();

        // Delete all related records first
        var toothRecords = await _database!.Table<ToothRecord>()
            .Where(r => r.PatientId == patient.PatientID).ToListAsync();
        foreach (var r in toothRecords) await _database!.DeleteAsync(r);

        var histories = await _database!.Table<TreatmentHistory>()
            .Where(h => h.PatientId == patient.PatientID).ToListAsync();
        foreach (var h in histories) await _database!.DeleteAsync(h);

        var guardians = await _database!.Table<Guardian>()
            .Where(g => g.PatientID == patient.PatientID).ToListAsync();
        foreach (var g in guardians) await _database!.DeleteAsync(g);

        var medHist = await _database!.Table<MedicalHistory>()
            .Where(m => m.PatientID == patient.PatientID).ToListAsync();
        foreach (var m in medHist) await _database!.DeleteAsync(m);

        var allergies = await _database!.Table<Allergy>()
            .Where(a => a.PatientID == patient.PatientID).ToListAsync();
        foreach (var a in allergies) await _database!.DeleteAsync(a);

        var conditions = await _database!.Table<PatientCondition>()
            .Where(pc => pc.PatientID == patient.PatientID).ToListAsync();
        foreach (var pc in conditions) await _database!.DeleteAsync(pc);

        var images = await _database!.Table<CephalometricImage>()
            .Where(c => c.PatientId == patient.PatientID).ToListAsync();
        foreach (var img in images) await _database!.DeleteAsync(img);

        await _database!.DeleteAsync(patient);
    }

    public async Task<Patient> GetPatientById(int id)
    {
        await Init();
        return await _database!.Table<Patient>()
                               .Where(p => p.PatientID == id)
                               .FirstOrDefaultAsync();
    }

    // =========================
    // SUPABASE BOOKING SYNC
    // =========================

    /// <summary>
    /// Converts an incoming Supabase booking into a local Patient + sets
    /// ReasonForConsultation from the booked service. Skips duplicates
    /// by checking phone + name to avoid double-inserts on reconnect.
    /// Returns the new Patient's local ID, or the existing one if duplicate.
    /// </summary>
    public async Task<int> SyncBookingFromWeb(SupabaseBooking booking)
    {
        await Init();

        // Skip if already synced by Supabase booking ID
        var alreadySynced = await _database!.Table<SyncedBooking>()
            .Where(s => s.SupabaseId == booking.Id)
            .FirstOrDefaultAsync();

        if (alreadySynced != null)
        {
            System.Diagnostics.Debug.WriteLine(
                $"[Sync] Already synced booking {booking.Id}, skipping.");
            return 0;
        }

        // Split full name
        var parts = (booking.FullName ?? "").Trim().Split(' ', 2);
        string firstName = parts.Length > 0 ? parts[0] : "";
        string lastName = parts.Length > 1 ? parts[1] : "";

        // Insert patient
        var patient = new Patient
        {
            FirstName = firstName,
            LastName = lastName,
            MobileNo = booking.Phone ?? string.Empty,
            Email = booking.Email ?? string.Empty,
            DateOfBirth = booking.DateOfBirth.HasValue
                                        ? booking.DateOfBirth.Value.ToString("yyyy-MM-dd")
                                        : string.Empty,
            ReasonForConsultation = booking.Service ?? string.Empty,
            DateRegistered = DateTime.Now.ToString("yyyy-MM-dd"),
            ReferredBy = "Online Booking"
        };
        await _database!.InsertAsync(patient);

        // Mark this Supabase booking as synced
        await _database!.InsertAsync(new SyncedBooking
        {
            SupabaseId = booking.Id,
            SyncedAt = DateTime.Now
        });

        System.Diagnostics.Debug.WriteLine(
            $"[Sync] Patient added: {patient.FullName} from booking {booking.Id}");

        return patient.PatientID;
    }
    // ══════════════════════════════════════════
    // GUARDIAN
    // ══════════════════════════════════════════
    public async Task<Guardian?> GetGuardianByPatient(int patientId)
    {
        await Init();
        return await _database!.Table<Guardian>()
            .Where(g => g.PatientID == patientId).FirstOrDefaultAsync();
    }
    public async Task SaveGuardian(Guardian g)
    {
        await Init();
        var ex = await GetGuardianByPatient(g.PatientID);
        if (ex is null) await _database!.InsertAsync(g);
        else { g.GuardianID = ex.GuardianID; await _database!.UpdateAsync(g); }
    }

    // ══════════════════════════════════════════
    // MEDICAL HISTORY
    // ══════════════════════════════════════════
    public async Task<MedicalHistory?> GetMedicalHistory(int patientId)
    {
        await Init();
        return await _database!.Table<MedicalHistory>()
            .Where(m => m.PatientID == patientId).FirstOrDefaultAsync();
    }
    public async Task SaveMedicalHistory(MedicalHistory m)
    {
        await Init();
        var ex = await GetMedicalHistory(m.PatientID);
        m.LastUpdated = DateTime.Now.ToString("yyyy-MM-dd");
        if (ex is null) await _database!.InsertAsync(m);
        else { m.MedicalHistoryID = ex.MedicalHistoryID; await _database!.UpdateAsync(m); }
    }

    // ══════════════════════════════════════════
    // ALLERGY
    // ══════════════════════════════════════════
    public async Task<Allergy?> GetAllergy(int patientId)
    {
        await Init();
        return await _database!.Table<Allergy>()
            .Where(a => a.PatientID == patientId).FirstOrDefaultAsync();
    }
    public async Task SaveAllergy(Allergy a)
    {
        await Init();
        var ex = await GetAllergy(a.PatientID);
        if (ex is null) await _database!.InsertAsync(a);
        else { a.AllergyID = ex.AllergyID; await _database!.UpdateAsync(a); }
    }

    // ══════════════════════════════════════════
    // MEDICAL CONDITIONS
    // ══════════════════════════════════════════
    public async Task<List<MedicalCondition>> GetAllConditions()
    {
        await Init();
        return await _database!.Table<MedicalCondition>().ToListAsync();
    }
    public async Task<List<PatientCondition>> GetPatientConditions(int patientId)
    {
        await Init();
        return await _database!.Table<PatientCondition>()
            .Where(pc => pc.PatientID == patientId).ToListAsync();
    }
    public async Task SavePatientConditions(int patientId, List<int> conditionIds)
    {
        await Init();
        var existing = await GetPatientConditions(patientId);
        foreach (var e in existing) await _database!.DeleteAsync(e);
        foreach (var id in conditionIds)
            await _database!.InsertAsync(new PatientCondition { PatientID = patientId, ConditionID = id });
    }
    public async Task EnsureDefaultConditions()
    {
        await Init();
        if (await _database!.Table<MedicalCondition>().CountAsync() > 0) return;
        var defaults = new[]
        {
            "Diabetes","Hypertension","Heart Disease","Asthma","Epilepsy / Seizures",
            "Thyroid Disorder","Kidney Disease","Liver Disease","Blood Disorder",
            "Arthritis","Osteoporosis","Stroke","Cancer","Tuberculosis",
            "Hepatitis","HIV / AIDS","Psychiatric Disorder","Other"
        };
        await _database!.InsertAllAsync(defaults.Select(n => new MedicalCondition { ConditionName = n }));
    }

    // =========================
    // SERVICE CRUD
    // =========================

    public async Task<List<ServiceModel>> GetServices()
    {
        await Init();
        return await _database!.Table<ServiceModel>()
                               .Where(s => !s.IsDeleted)
                               .ToListAsync();
    }

    public async Task AddService(ServiceModel service)
    {
        await Init();
        await _database!.InsertAsync(service);
    }

    public async Task DeleteService(ServiceModel service)
    {
        await Init();
        service.IsDeleted = true;
        await _database!.UpdateAsync(service);
    }

    public async Task UpdateService(ServiceModel service)
    {
        await Init();
        await _database!.UpdateAsync(service);
    }

    // =========================
    // USER CRUD
    // =========================

    public async Task<List<User>> GetUsers()
    {
        await Init();
        return await _database!.Table<User>()
                               .Where(u => !u.IsDeleted)
                               .ToListAsync();
    }

    public async Task AddUser(User user)
    {
        await Init();
        await _database!.InsertAsync(user);
    }

    public async Task<int> DeleteUser(User user)
    {
        await Init();
        user.IsDeleted = true;
        return await _database!.UpdateAsync(user);
    }

    public async Task UpdateUser(User user)
    {
        await Init();
        await _database!.UpdateAsync(user);
    }

    // =========================
    // TOOTH RECORD CRUD
    // =========================

    public async Task<List<ToothRecord>> GetToothRecordsForPatient(int patientId)
    {
        await Init();
        return await _database!.Table<ToothRecord>()
                               .Where(r => r.PatientId == patientId)
                               .ToListAsync();
    }

    public async Task SaveToothRecord(ToothRecord record)
    {
        await Init();
        var existing = await _database!.Table<ToothRecord>()
            .Where(r => r.PatientId == record.PatientId && r.ToothNumber == record.ToothNumber)
            .FirstOrDefaultAsync();

        record.LastUpdated = DateTime.UtcNow.ToString("yyyy-MM-dd");
        if (existing is null)
            await _database!.InsertAsync(record);
        else
        {
            record.Id = existing.Id;
            await _database!.UpdateAsync(record);
        }
    }

    public async Task DeleteToothRecord(int patientId, int toothNumber)
    {
        await Init();
        var existing = await _database!.Table<ToothRecord>()
            .Where(r => r.PatientId == patientId && r.ToothNumber == toothNumber)
            .FirstOrDefaultAsync();
        if (existing is not null)
            await _database!.DeleteAsync(existing);
    }

    // =========================
    // TREATMENT HISTORY CRUD
    // =========================

    public async Task<List<TreatmentHistory>> GetTreatmentHistoryForPatient(int patientId)
    {
        await Init();
        var list = await _database!.Table<TreatmentHistory>()
                                   .Where(h => h.PatientId == patientId)
                                   .ToListAsync();
        list.Sort((a, b) => string.Compare(b.Timestamp, a.Timestamp, StringComparison.Ordinal));
        return list;
    }

    public async Task AddTreatmentHistory(TreatmentHistory entry)
    {
        await Init();
        entry.Timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
        await _database!.InsertAsync(entry);
    }

    public async Task DeleteTreatmentHistoryForPatient(int patientId)
    {
        await Init();
        var entries = await _database!.Table<TreatmentHistory>()
                                      .Where(h => h.PatientId == patientId)
                                      .ToListAsync();
        foreach (var e in entries)
            await _database!.DeleteAsync(e);
    }

    // =========================
    // CEPHALOMETRIC IMAGE CRUD
    // =========================

    public async Task<CephalometricImage?> GetActiveCephalometricImage(int patientId)
    {
        await Init();
        return await _database!.Table<CephalometricImage>()
                               .Where(c => c.PatientId == patientId && c.IsActive)
                               .FirstOrDefaultAsync();
    }

    public async Task SaveCephalometricImage(CephalometricImage newImage)
    {
        await Init();
        var existing = await GetActiveCephalometricImage(newImage.PatientId);
        if (existing != null)
        {
            existing.IsActive = false;
            await _database!.UpdateAsync(existing);
        }
        newImage.IsActive = true;
        newImage.UploadedDate = DateTime.Now.ToString("yyyy-MM-dd");
        await _database!.InsertAsync(newImage);
    }

    // =========================
    // SUPPLY ITEM CRUD
    // =========================

    public async Task<List<SupplyItem>> GetSupplyItems()
    {
        await Init();
        return await _database!.Table<SupplyItem>()
                               .Where(s => !s.IsDeleted)
                               .ToListAsync();
    }

    public async Task<SupplyItem?> GetSupplyItemById(int id)
    {
        await Init();
        return await _database!.Table<SupplyItem>().Where(s => s.Id == id).FirstOrDefaultAsync();
    }

    public async Task<int> AddSupplyItem(SupplyItem item)
    {
        await Init();
        item.AddedDate = DateTime.Now.ToString("yyyy-MM-dd");
        await _database!.InsertAsync(item);
        return item.Id;
    }

    public async Task UpdateSupplyItem(SupplyItem item)
    {
        await Init();
        await _database!.UpdateAsync(item);
    }

    public async Task DeleteSupplyItem(SupplyItem item)
    {
        await Init();
        item.IsDeleted = true;
        await _database!.UpdateAsync(item);
    }

    public async Task<List<SupplyItem>> GetLowStockItems()
    {
        await Init();
        var all = await _database!.Table<SupplyItem>().ToListAsync();
        return all.Where(s => s.QuantityInPieces <= s.MinimumStockPieces).ToList();
    }

    // =========================
    // SUPPLY STOCK LOG CRUD
    // =========================

    public async Task<List<SupplyStockLog>> GetLogsForSupplyItem(int supplyItemId)
    {
        await Init();
        var list = await _database!.Table<SupplyStockLog>()
                                   .Where(l => l.SupplyItemId == supplyItemId)
                                   .ToListAsync();
        list.Sort((a, b) => string.Compare(b.Timestamp, a.Timestamp, StringComparison.Ordinal));
        return list;
    }

    public async Task ApplyStockChange(int supplyItemId, int changeInPieces, string changeType,
                                       string note = "", int patientId = 0, string patientName = "")
    {
        await Init();
        var item = await GetSupplyItemById(supplyItemId);
        if (item == null) return;

        item.QuantityInPieces = Math.Max(0, item.QuantityInPieces + changeInPieces);
        await _database!.UpdateAsync(item);

        var log = new SupplyStockLog
        {
            SupplyItemId = supplyItemId,
            ChangeInPieces = changeInPieces,
            ChangeType = changeType,
            Note = note,
            PatientId = patientId,
            PatientName = patientName,
            StockAfterChange = item.QuantityInPieces,
            Timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
        };
        await _database!.InsertAsync(log);
    }

    // =========================
    // PENDING APPOINTMENTS
    // =========================

    public async Task AddPendingAppointment(Appointment appt)
    {
        await Init();
        // Don't add duplicate Supabase bookings
        var existing = await _database!.Table<Appointment>()
            .Where(p => p.SupabaseBookingId == appt.SupabaseBookingId)
            .FirstOrDefaultAsync();
        if (existing != null) return;

        appt.ReceivedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
        await _database!.InsertAsync(appt);
    }

    public async Task<List<Appointment>> GetPendingAppointments()
    {
        await Init();
        return await _database!.Table<Appointment>()
            .Where(p => p.Status == "pending")
            .ToListAsync();
    }

    public async Task ApproveAppointment(Appointment appt)
    {
        await Init();

        // Convert to real patient
        var parts = appt.FullName.Trim().Split(' ', 2);
        var patient = new Patient
        {
            FirstName = parts.Length > 0 ? parts[0] : appt.FullName,
            LastName = parts.Length > 1 ? parts[1] : "",
            MobileNo = appt.Phone,
            Email = appt.Email,
            DateOfBirth = appt.DateOfBirth,
            ReasonForConsultation = appt.Service,
            DateRegistered = DateTime.Now.ToString("yyyy-MM-dd"),
            ReferredBy = "Online Booking"
        };
        await _database!.InsertAsync(patient);

        // Add treatment history entry
        var history = new TreatmentHistory
        {
            PatientId = patient.PatientID,
            Timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
            Notes = $"Appointment: {appt.AppointmentDate}" +
                          (string.IsNullOrWhiteSpace(appt.Notes) ? "" : $"\nNote: {appt.Notes}")
        };
        await AddTreatmentHistory(history);

        // Mark appointment as approved
        appt.Status = "approved";
        await _database!.UpdateAsync(appt);
    }

    public async Task RejectAppointment(Appointment appt)
    {
        await Init();
        appt.Status = "rejected";
        await _database!.UpdateAsync(appt);
    }

    // =========================
    // SUPABASE PATIENT SYNC
    // =========================

    public async Task SyncPatientFromSupabase(SupabasePatient sp)
    {
        await Init();
        try
        {
            if (string.IsNullOrWhiteSpace(sp.FirstName))
            {
                System.Diagnostics.Debug.WriteLine($"[SyncPatient] Skipped blank. Id={sp.Id}");
                return;
            }

            Patient? existing = null;

            // Find by SupabaseId first
            if (!string.IsNullOrEmpty(sp.Id))
                existing = await _database!.Table<Patient>()
                    .Where(p => p.SupabaseId == sp.Id)
                    .FirstOrDefaultAsync();

            // Fallback: match by name + phone
            if (existing == null && !string.IsNullOrEmpty(sp.Phone))
                existing = await _database!.Table<Patient>()
                    .Where(p => p.FirstName == sp.FirstName
                             && p.MobileNo == sp.Phone)
                    .FirstOrDefaultAsync();

            if (existing != null)
            {
                // Update existing
                existing.FirstName = sp.FirstName;
                existing.LastName = sp.LastName ?? "";
                existing.Nickname = sp.Nickname ?? "";
                existing.Gender = sp.Gender ?? "";
                existing.DateOfBirth = sp.DateOfBirth.HasValue
                                                    ? sp.DateOfBirth.Value.ToString("yyyy-MM-dd") : "";
                existing.Nationality = sp.Nationality ?? "";
                existing.Religion = sp.Religion ?? "";
                existing.Occupation = sp.Occupation ?? "";
                existing.Address = sp.Address ?? "";
                existing.MobileNo = sp.Phone ?? "";
                existing.HomeNo = sp.HomeNo ?? "";
                existing.OfficeNo = sp.OfficeNo ?? "";
                existing.FaxNo = sp.FaxNo ?? "";
                existing.Email = sp.Email ?? "";
                existing.ReferredBy = sp.ReferredBy ?? "";
                existing.ReasonForConsultation = sp.ReasonForConsultation ?? "";
                existing.DentalInsurance = sp.DentalInsurance ?? "";
                existing.InsuranceEffectiveDate = sp.InsuranceEffectiveDate.HasValue
                                                    ? sp.InsuranceEffectiveDate.Value.ToString("yyyy-MM-dd") : "";
                existing.SupabaseId = sp.Id;
                await _database!.UpdateAsync(existing);

                // Update related tables
                await SyncRelatedFromSupabase(existing.PatientID, sp);
                System.Diagnostics.Debug.WriteLine(
                    $"[SyncPatient] Updated PatientID={existing.PatientID}");
            }
            else
            {
                // Insert new
                var patient = new Patient
                {
                    FirstName = sp.FirstName,
                    LastName = sp.LastName ?? "",
                    Nickname = sp.Nickname ?? "",
                    Gender = sp.Gender ?? "",
                    DateOfBirth = sp.DateOfBirth.HasValue
                                                ? sp.DateOfBirth.Value.ToString("yyyy-MM-dd") : "",
                    Nationality = sp.Nationality ?? "",
                    Religion = sp.Religion ?? "",
                    Occupation = sp.Occupation ?? "",
                    Address = sp.Address ?? "",
                    MobileNo = sp.Phone ?? "",
                    HomeNo = sp.HomeNo ?? "",
                    OfficeNo = sp.OfficeNo ?? "",
                    FaxNo = sp.FaxNo ?? "",
                    Email = sp.Email ?? "",
                    ReferredBy = sp.ReferredBy ?? "",
                    ReasonForConsultation = sp.ReasonForConsultation ?? "",
                    DentalInsurance = sp.DentalInsurance ?? "",
                    InsuranceEffectiveDate = sp.InsuranceEffectiveDate.HasValue
                                                ? sp.InsuranceEffectiveDate.Value.ToString("yyyy-MM-dd") : "",
                    DateRegistered = sp.DateRegistered != default
                                                ? sp.DateRegistered.ToString("yyyy-MM-dd")
                                                : DateTime.Now.ToString("yyyy-MM-dd"),
                    SupabaseId = sp.Id
                };
                await _database!.InsertAsync(patient);
                await SyncRelatedFromSupabase(patient.PatientID, sp);
                System.Diagnostics.Debug.WriteLine(
                    $"[SyncPatient] Inserted PatientID={patient.PatientID} from Supabase {sp.Id}");
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[SyncPatient] Error: {ex.Message}");
        }
    }

    // Syncs guardian, medical history, allergies from a SupabasePatient
    private async Task SyncRelatedFromSupabase(int patientId, SupabasePatient sp)
    {
        try
        {
            if (!string.IsNullOrEmpty(sp.GuardianName))
                await SaveGuardian(new Guardian
                {
                    PatientID = patientId,
                    GuardianName = sp.GuardianName,
                    RelationshipToPatient = sp.GuardianRelationship ?? "",
                    Occupation = sp.GuardianOccupation ?? "",
                    MobileNo = sp.GuardianMobile ?? ""
                });

            await SaveMedicalHistory(new MedicalHistory
            {
                PatientID = patientId,
                BloodType = sp.BloodType ?? "",
                BloodPressure = sp.BloodPressure ?? "",
                BleedingTime = sp.BleedingTime ?? "",
                PhysicianName = sp.PhysicianName ?? "",
                IsGoodHealth = sp.GoodHealth,
                IsPregnant = sp.Pregnant,
                UnderMedicalTreatment = sp.UnderTreatment,
                MedicationDetails = sp.MedicationDetails ?? "",
                HasBeenHospitalized = sp.Hospitalized,
                HospitalizationDetails = sp.HospitalizationDetails ?? "",
                UsesTobacco = sp.UsesTobacco,
                UsesAlcohol = sp.UsesAlcohol,
                TakingMedications = sp.TakingMedications,
                PreviousDentist = sp.PreviousDentist ?? "",
                LastDentalVisit = sp.LastDentalVisit ?? ""
            });

            await SaveAllergy(new Allergy
            {
                PatientID = patientId,
                HasLatexAllergy = sp.LatexAllergy,
                HasAspirinAllergy = sp.AspirinAllergy,
                HasPenicillinAllergy = sp.PenicillinAllergy,
                HasSulfaAllergy = sp.SulfaAllergy,
                HasLocalAnestheticAllergy = sp.LocalAnestheticAllergy,
                OtherAllergy = sp.OtherAllergy ?? ""
            });
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[SyncRelated] Error: {ex.Message}");
        }
    }

    // Called once on startup — links existing local patients to their Supabase rows by name+phone match
    public async Task BackfillSupabaseIds(List<SupabasePatient> supabasePatients)
    {
        await Init();
        foreach (var sp in supabasePatients)
        {
            if (string.IsNullOrEmpty(sp.Id)) continue;

            // Find local patient with same first name + phone that has no SupabaseId yet
            var local = await _database!.Table<Patient>()
                .Where(p => p.FirstName == sp.FirstName
                         && p.MobileNo == (sp.Phone ?? "")
                         && p.SupabaseId == "")
                .FirstOrDefaultAsync();

            if (local != null)
            {
                local.SupabaseId = sp.Id;
                await _database!.UpdateAsync(local);
                System.Diagnostics.Debug.WriteLine(
                    $"[Backfill] Linked PatientID={local.PatientID} → SupabaseId={sp.Id}");
            }
        }
    }

    // ── APPOINTMENT ENTRIES ────────────────────────────────────────

    public async Task<List<AppointmentEntry>> GetAppointmentsForDate(DateTime date)
    {
        await Init();
        var dateStr = date.ToString("yyyy-MM-dd");
        var all = await _database!.Table<AppointmentEntry>().ToListAsync();
        return all.Where(a => a.AppointmentDateTime.StartsWith(dateStr))
                  .OrderBy(a => a.AppointmentDateTime)
                  .ToList();
    }

    public async Task<List<AppointmentEntry>> GetAppointmentsForWeek(DateTime weekStart)
    {
        await Init();
        var weekEnd = weekStart.AddDays(7);
        var all = await _database!.Table<AppointmentEntry>().ToListAsync();
        return all.Where(a =>
        {
            if (!DateTime.TryParse(a.AppointmentDateTime, out var dt)) return false;
            return dt >= weekStart && dt < weekEnd;
        })
        .OrderBy(a => a.AppointmentDateTime)
        .ToList();
    }

    public async Task AddAppointmentEntry(AppointmentEntry entry)
    {
        await Init();
        // Prevent duplicates by SupabaseBookingId
        if (!string.IsNullOrEmpty(entry.SupabaseBookingId))
        {
            var existing = await _database!.Table<AppointmentEntry>()
                .Where(a => a.SupabaseBookingId == entry.SupabaseBookingId)
                .FirstOrDefaultAsync();
            if (existing != null) return;
        }
        await _database!.InsertAsync(entry);
    }

    public async Task UpdateAppointmentStatus(int id, string status)
    {
        await Init();
        var entry = await _database!.Table<AppointmentEntry>()
            .Where(a => a.Id == id).FirstOrDefaultAsync();
        if (entry == null) return;
        entry.Status = status;
        await _database!.UpdateAsync(entry);
    }

    public async Task DeleteAppointmentEntry(AppointmentEntry entry)
    {
        await Init();
        await _database!.DeleteAsync(entry);
    }

    public async Task CleanupPastLocalAppointmentsAsync()
    {
        await Init();
        try
        {
            var now = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");

            // Delete completed and cancelled past appointments from local SQLite
            var deleted = await _database!.ExecuteAsync(
                "DELETE FROM AppointmentEntry " +
                "WHERE Status IN ('completed', 'cancelled') " +
                "AND AppointmentDateTime < ?", now);

            System.Diagnostics.Debug.WriteLine(
                $"[LocalCleanup] Deleted {deleted} local past appointments");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine(
                $"[LocalCleanup] Error: {ex.Message}");
        }
    }
}
