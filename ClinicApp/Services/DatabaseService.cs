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

            string dbPath = Path.Combine(FileSystem.AppDataDirectory, "clinic.db3");
            _database = new SQLiteAsyncConnection(dbPath);

            // Create all tables if they don't exist yet
            await _database.CreateTableAsync<Patient>();
            await _database.CreateTableAsync<ServiceModel>();
            await _database.CreateTableAsync<ServicePackage>();
            await _database.CreateTableAsync<User>();
            await _database.CreateTableAsync<ToothRecord>();

            try { await _database.ExecuteAsync("ALTER TABLE User ADD COLUMN ContactNo TEXT"); } catch { }
            try { await _database.ExecuteAsync("ALTER TABLE User ADD COLUMN Email TEXT"); } catch { }
            try { await _database.ExecuteAsync("ALTER TABLE User ADD COLUMN IsActive INTEGER DEFAULT 1"); } catch { }

            // Seed default users on first run
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

}
