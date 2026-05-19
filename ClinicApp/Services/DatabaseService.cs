using ClinicApp.Models;
using SQLite;

namespace ClinicApp.Services;

public class DatabaseService
{
    // SQLite async connection, initialized once via Init()
    SQLiteAsyncConnection? _database;

    public async Task Init()
    {
        try
        {
            if (_database != null)
                return;
            // This saves it to the "Downloads" folder on the Android Emulator
            //string dbPath = Path.Combine("/storage/emulated/0/Download", "clinicmob.db3");

            // this saves in windows
            string dbPath = Path.Combine(FileSystem.AppDataDirectory, "clinic.db3");

            //MESSAGE FOR FINDING THE DATABASE PATH
            await Shell.Current.DisplayAlert(
          "DB PATH",
          dbPath,
          "OK");

            _database = new SQLiteAsyncConnection(dbPath, SQLiteOpenFlags.ReadWrite | SQLiteOpenFlags.Create | SQLiteOpenFlags.SharedCache);
            await _database.ExecuteAsync("PRAGMA journal_mode=WAL;");
            await _database.ExecuteAsync("PRAGMA busy_timeout=3000;");

         

            //_database = new SQLiteAsyncConnection(dbPath);

            // Create all tables if they don't exist yet
            await _database.CreateTableAsync<Patient>();
            await _database.CreateTableAsync<ServiceModel>();
            await _database.CreateTableAsync<ServicePackage>();
            await _database.CreateTableAsync<User>();
            await _database.CreateTableAsync<ToothRecord>();
            await _database.CreateTableAsync<CephalometricImage>();
            await _database.CreateTableAsync<TreatmentHistory>();
            await _database.CreateTableAsync<SupplyStockLog>();
            await _database.CreateTableAsync<SupplyItem>();

            // Migrate: add new columns to User table if they don't exist yet
            // Safe to run on existing installs — SQLite ignores duplicate columns
            //try { await _database.ExecuteAsync("ALTER TABLE User ADD COLUMN ContactNo TEXT"); } catch { }
            //try { await _database.ExecuteAsync("ALTER TABLE User ADD COLUMN Email TEXT"); } catch { }
            //try { await _database.ExecuteAsync("ALTER TABLE User ADD COLUMN IsActive INTEGER DEFAULT 1"); } catch { }

            // Seed default users on first run — both Active by default
            var userCount = await _database.Table<User>().CountAsync();
            if (userCount == 0)
            {
                var defaultUsers = new List<User>
                {
                    new User { FullName = "Dr. Full Name", Username = "dentist1", Password = "123", Role = "Dentist", IsActive = true },
                    new User { FullName = "Assistant Name", Username = "staff1", Password = "123", Role = "Assistant", IsActive = true }
                };
                await _database.InsertAllAsync(defaultUsers);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine(ex.Message);
        }
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
        foreach (var r in toothRecords)
            await _database!.DeleteAsync(r);

        var histories = await _database!.Table<TreatmentHistory>()
            .Where(h => h.PatientId == patient.PatientID).ToListAsync();
        foreach (var h in histories)
            await _database!.DeleteAsync(h);

        var images = await _database!.Table<CephalometricImage>()
            .Where(c => c.PatientId == patient.PatientID).ToListAsync();
        foreach (var img in images)
            await _database!.DeleteAsync(img);

        // Now safe to delete the patient
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
    // SERVICE CRUD
    // =========================

    public async Task<List<ServiceModel>> GetServices()
    {
        await Init();
        return await _database!.Table<ServiceModel>().ToListAsync();
    }

    public async Task AddService(ServiceModel service)
    {
        await Init();
        await _database!.InsertAsync(service);
    }

    public async Task DeleteService(ServiceModel service)
    {
        await Init();
        await _database!.DeleteAsync(service);
    }

    public async Task UpdateService(ServiceModel service)
    {
        await Init();
        await _database!.UpdateAsync(service);
    }

    // =========================
    // SERVICE PACKAGE CRUD
    // =========================

    public async Task<List<ServicePackage>> GetServicePackages()
    {
        await Init();
        return await _database!.Table<ServicePackage>().ToListAsync();
    }

    public async Task AddServicePackage(ServicePackage package)
    {
        await Init();
        await _database!.InsertAsync(package);
    }

    public async Task UpdateServicePackage(ServicePackage package)
    {
        await Init();
        await _database!.UpdateAsync(package);
    }

    public async Task DeleteServicePackage(ServicePackage package)
    {
        await Init();
        await _database!.DeleteAsync(package);
    }

    // =========================
    // USER CRUD
    // =========================

    public async Task<List<User>> GetUsers()
    {
        await Init();
        return await _database!.Table<User>().ToListAsync();
    }

    public async Task AddUser(User user)
    {
        await Init();
        await _database!.InsertAsync(user);
    }

    public async Task<int> DeleteUser(User user)
    {
        await Init();
        return await _database!.DeleteAsync(user);
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

    /// <summary>Returns all history entries for a patient, newest first.</summary>
    public async Task<List<TreatmentHistory>> GetTreatmentHistoryForPatient(int patientId)
    {
        await Init();
        var list = await _database!.Table<TreatmentHistory>()
                                   .Where(h => h.PatientId == patientId)
                                   .ToListAsync();
        list.Sort((a, b) => string.Compare(b.Timestamp, a.Timestamp, StringComparison.Ordinal));
        return list;
    }

    /// <summary>Appends a new history entry (never updates, always inserts).</summary>
    public async Task AddTreatmentHistory(TreatmentHistory entry)
    {
        await Init();
        entry.Timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
        await _database!.InsertAsync(entry);
    }

    /// <summary>Deletes all history for a patient (e.g. when patient is deleted).</summary>
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

    // Gets the current active (non-archived) image for a patient
    public async Task<CephalometricImage?> GetActiveCephalometricImage(int patientId)
    {
        await Init();
        return await _database!.Table<CephalometricImage>()
                               .Where(c => c.PatientId == patientId && c.IsActive)
                               .FirstOrDefaultAsync();
    }

    // Saves a new image. Archives the old active image first if one exists.
    public async Task SaveCephalometricImage(CephalometricImage newImage)
    {
        await Init();

        // Archive the existing active image for this patient
        var existing = await GetActiveCephalometricImage(newImage.PatientId);
        if (existing != null)
        {
            existing.IsActive = false;
            await _database!.UpdateAsync(existing);
        }

        // Insert the new active image with today's date
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
        return await _database!.Table<SupplyItem>().ToListAsync();
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
        var logs = await _database!.Table<SupplyStockLog>()
                                   .Where(l => l.SupplyItemId == item.Id)
                                   .ToListAsync();
        foreach (var l in logs) await _database!.DeleteAsync(l);
        await _database!.DeleteAsync(item);
    }

    /// <summary>Returns all supply items that are at or below their minimum stock level.</summary>
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

    /// <summary>
    /// Adjusts SupplyItem.QuantityInPieces and appends a log entry atomically.
    /// changeInPieces: positive = restock, negative = consume.
    /// </summary>
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
            PatientId = patientId,
            PatientName = patientName,
            StockAfterChange = item.QuantityInPieces,
            
            Timestamp = DateTime.Now.ToString("yyyy-MM-dd"),
        };
        await _database!.InsertAsync(log);
    }

}
