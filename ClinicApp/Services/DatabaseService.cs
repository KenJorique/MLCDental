using ClinicApp.Models;
using SQLite;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ClinicApp.Services;

public class DatabaseService
{
    SQLiteAsyncConnection _database;

    public async Task Init()
    {
        try
        {
            if (_database != null)
                return;

            string dbPath = Path.Combine(FileSystem.AppDataDirectory, "clinic.db3");
            

            // Create the User table
           


            _database = new SQLiteAsyncConnection(dbPath);

            await _database.CreateTableAsync<Patient>();
            await _database.CreateTableAsync<ServiceModel>();
            await _database.CreateTableAsync<User>();

            var userCount = await _database.Table<User>().CountAsync();
            if (userCount == 0)
            {
                var defaultUsers = new List<User>
        {
            new User { FullName = "Dr. Full Name", Username = "dentist1", Password = "123", Role = "Dentist" },
            new User { FullName = "Assistant Name", Username = "staff1", Password = "123", Role = "Assistant" }
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
        return await _database.Table<Patient>().ToListAsync();
    }

    public async Task AddPatient(Patient patient)
    {
        try
        {
            await Init();

            int result = await _database.InsertAsync(patient);

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
        await _database.UpdateAsync(patient);
    }

    public async Task DeletePatient(Patient patient)
    {
        await Init();
        await _database.DeleteAsync(patient);
    }

    public async Task<Patient> GetPatientById(int id)
    {
        await Init(); // Always ensure the connection is initialized
        return await _database.Table<Patient>()
                              .Where(p => p.PatientID == id)
                              .FirstOrDefaultAsync();
    }




    // =========================
    // SERVICE CRUD
    // =========================

    public async Task<List<ServiceModel>> GetServices()
    {
        await Init();
        return await _database.Table<ServiceModel>().ToListAsync();
    }

    public async Task AddService(ServiceModel service)
    {
        await Init();
        await _database.InsertAsync(service);
    }

    public async Task DeleteService(ServiceModel service)
    {
        await Init();
        await _database.DeleteAsync(service);
    }

    public async Task UpdateService(ServiceModel service)
    {
        await Init();
        await _database.UpdateAsync(service);
    }

    // =========================
    // USER CRUD
    // =========================

    public async Task<List<User>> GetUsers()
    {
        await Init();
        return await _database.Table<User>().ToListAsync();
    }

    public async Task AddUser(User user)
    {
        await Init();
        await _database.InsertAsync(user);
    }

    public async Task<int> DeleteUser(User user)
    {
        await Init();
        return await _database.DeleteAsync(user);
    }

    public async Task UpdateUser(User user)
    {
        await Init();
        await _database.UpdateAsync(user);
    }
}
