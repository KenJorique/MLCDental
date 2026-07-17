using ClinicApp.Models;
using ClinicApp.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;

namespace ClinicApp.ViewModels.PatientsRelatedVM;

[QueryProperty(nameof(PatientId), "PatientId")]
public partial class AddPatientViewModel : ObservableObject
{
    readonly DatabaseService _db;
    readonly SupabaseDataService _supabase;

    // Tracks the Supabase UUID when editing an existing patient
    private string _supabaseId = string.Empty;

    public AddPatientViewModel(DatabaseService db, SupabaseDataService supabase)
    {
        _db = db;
        _supabase = supabase;
    }

    [ObservableProperty] int patientId;
    [ObservableProperty] string pageTitle = "Add New Patient";
    [ObservableProperty] bool isBusy;

    // ── Section A: Personal Info ──────────────────────────────────
    [ObservableProperty] string firstName = string.Empty;
    [ObservableProperty] string lastName = string.Empty;
    [ObservableProperty] string nickname = string.Empty;
    [ObservableProperty] string selectedGender = "Male";
    [ObservableProperty] DateTime dateOfBirth = DateTime.Today.AddYears(-20);
    [ObservableProperty] string nationality = string.Empty;
    [ObservableProperty] string religion = string.Empty;
    [ObservableProperty] string occupation = string.Empty;
    [ObservableProperty] string address = string.Empty;
    [ObservableProperty] DateTime dateRegistered = DateTime.Today;

    // ── Section B: Contact ────────────────────────────────────────
    [ObservableProperty] string mobileNo = string.Empty;
    [ObservableProperty] string homeNo = string.Empty;
    [ObservableProperty] string faxNo = string.Empty;
    [ObservableProperty] string officeNo = string.Empty;
    [ObservableProperty] string email = string.Empty;

    // ── Section C: Guardian ───────────────────────────────────────
    [ObservableProperty] string guardianName = string.Empty;
    [ObservableProperty] string guardianRelationship = string.Empty;
    [ObservableProperty] string guardianOccupation = string.Empty;
    [ObservableProperty] string guardianMobileNo = string.Empty;

    // ── Section D: Medical History ────────────────────────────
    [ObservableProperty] string bloodType = string.Empty;

    // Health status flags
    [ObservableProperty] bool isGoodHealth = true;
    [ObservableProperty] bool isPregnant;
    [ObservableProperty] bool underMedicalTreatment;
    [ObservableProperty] string medicationDetails = string.Empty;
    [ObservableProperty] bool hasBeenHospitalized;
    [ObservableProperty] string hospitalizationDetails = string.Empty;
    [ObservableProperty] bool usesTobacco;
    [ObservableProperty] bool takingMedications;

    // ── Section E: Allergies ──────────────────────────────────────
    [ObservableProperty] bool hasLatexAllergy;
    [ObservableProperty] bool hasAspirinAllergy;
    [ObservableProperty] bool hasPenicillinAllergy;
    [ObservableProperty] bool hasSulfaAllergy;
    [ObservableProperty] bool hasLocalAnestheticAllergy;
    [ObservableProperty] string otherAllergy = string.Empty;

    // ── Section F: Medical Conditions ─────────────────────────────
    public ObservableCollection<ConditionCheckItem> Conditions { get; } = new();
    [ObservableProperty] string otherCondition = string.Empty;

    public List<string> GenderOptions { get; } = new() { "Male", "Female", "Other" };
    public List<string> BloodTypeOptions { get; } = new() { "A+", "A-", "B+", "B-", "AB+", "AB-", "O+", "O-", "Unknown" };
    public List<string> RelationshipOptions { get; } = new() { "Parent", "Spouse", "Sibling", "Child", "Grandparent", "Guardian", "Other" };

    public bool IsMinor
    {
        get
        {
            var today = DateTime.Today;
            var age = today.Year - DateOfBirth.Year;
            if (DateOfBirth.Date > today.AddYears(-age)) age--;
            return age < 18;
        }
    }

    partial void OnDateOfBirthChanged(DateTime value) =>
        OnPropertyChanged(nameof(IsMinor));

    // Shows Pregnant checkbox only for Female patients
    public bool IsFemale => SelectedGender?.Equals("Female", StringComparison.OrdinalIgnoreCase) ?? false;

    partial void OnSelectedGenderChanged(string value) =>
        OnPropertyChanged(nameof(IsFemale));

    public async Task InitializeAsync()
    {
        if (PatientId <= 0)
            await LoadConditionListAsync();
    }

    partial void OnPatientIdChanged(int value)
    {
        if (value > 0)
            MainThread.BeginInvokeOnMainThread(async () => await LoadForEditAsync(value));
    }

    private async Task LoadConditionListAsync()
    {
        await _db.EnsureDefaultConditions();
        var all = await _db.GetAllConditions();
        Conditions.Clear();
        foreach (var c in all.Where(c => c.ConditionName != "Other")
                             .OrderBy(c => c.ConditionName))
            Conditions.Add(new ConditionCheckItem
            {
                ConditionID = c.ConditionID,
                ConditionName = c.ConditionName
            });
    }

    private async Task LoadForEditAsync(int id)
    {
        IsBusy = true;
        try
        {
            PageTitle = "Edit Patient";
            var p = await _db.GetPatientById(id);
            if (p is null) return;

            // Store Supabase ID for update later
            _supabaseId = p.SupabaseId ?? string.Empty;
            System.Diagnostics.Debug.WriteLine(
                $"[Edit] Loaded patient {id}, SupabaseId={_supabaseId}");

            FirstName = p.FirstName;
            LastName = p.LastName;
            Nickname = p.Nickname;
            SelectedGender = p.Gender;
            if (DateTime.TryParse(p.DateOfBirth, out var dob)) DateOfBirth = dob;
            Nationality = p.Nationality;
            Religion = p.Religion;
            Occupation = p.Occupation;
            Address = p.Address;
            MobileNo = p.MobileNo;
            HomeNo = p.HomeNo;
            FaxNo = p.FaxNo;
            OfficeNo = p.OfficeNo;
            Email = p.Email;

            var g = await _db.GetGuardianByPatient(id);
            if (g is not null)
            {
                GuardianName = g.GuardianName;
                GuardianRelationship = g.RelationshipToPatient;
                GuardianOccupation = g.Occupation;
                GuardianMobileNo = g.MobileNo;
            }

            var m = await _db.GetMedicalHistory(id);
            if (m is not null)
            {
                BloodType = m.BloodType;
                IsGoodHealth = m.IsGoodHealth;
                IsPregnant = m.IsPregnant;
                UnderMedicalTreatment = m.UnderMedicalTreatment;
                MedicationDetails = m.MedicationDetails;
                HasBeenHospitalized = m.HasBeenHospitalized;
                HospitalizationDetails = m.HospitalizationDetails;
                UsesTobacco = m.UsesTobacco;
                TakingMedications = m.TakingMedications;
            }

            var a = await _db.GetAllergy(id);
            if (a is not null)
            {
                HasLatexAllergy = a.HasLatexAllergy;
                HasAspirinAllergy = a.HasAspirinAllergy;
                HasPenicillinAllergy = a.HasPenicillinAllergy;
                HasSulfaAllergy = a.HasSulfaAllergy;
                HasLocalAnestheticAllergy = a.HasLocalAnestheticAllergy;
                OtherAllergy = a.OtherAllergy;
            }

            await LoadConditionListAsync();
            var patientConds = await _db.GetPatientConditions(id);
            var selectedIds = patientConds.Select(pc => pc.ConditionID).ToHashSet();
            foreach (var item in Conditions)
                item.IsSelected = selectedIds.Contains(item.ConditionID);
        }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    async Task SavePatient()
    {
        var errors = new List<string>();
        if (string.IsNullOrWhiteSpace(FirstName) || string.IsNullOrWhiteSpace(LastName))
            errors.Add("• First and last name are required.");
        if (string.IsNullOrWhiteSpace(SelectedGender))
            errors.Add("• Gender is required.");
        if (string.IsNullOrWhiteSpace(Address))
            errors.Add("• Address is required.");
        if (string.IsNullOrWhiteSpace(MobileNo))
            errors.Add("• Mobile number is required.");
        if (IsMinor)
        {
            if (string.IsNullOrWhiteSpace(GuardianName))
                errors.Add("• Guardian name is required for patients under 18.");
            if (string.IsNullOrWhiteSpace(GuardianMobileNo))
                errors.Add("• Guardian mobile number is required for patients under 18.");
        }
        if (errors.Count > 0)
        {
            await Shell.Current.DisplayAlert("Required Fields Missing",
                string.Join("\n", errors), "OK");
            return;
        }

        IsBusy = true;
        try
        {
            // ── 1. Save to local SQLite ───────────────────────────────
            Patient p;
            if (PatientId > 0)
                p = await _db.GetPatientById(PatientId) ?? new Patient();
            else
                p = new Patient { DateRegistered = DateTime.Now.ToString("yyyy-MM-dd") };

            p.FirstName = FirstName.Trim();
            p.LastName = LastName.Trim();
            p.Nickname = Nickname.Trim();
            p.Gender = SelectedGender;
            p.DateOfBirth = DateOfBirth.ToString("yyyy-MM-dd");
            p.Nationality = Nationality.Trim();
            p.Religion = Religion.Trim();
            p.Occupation = Occupation.Trim();
            p.Address = Address.Trim();
            p.MobileNo = MobileNo.Trim();
            p.HomeNo = HomeNo.Trim();
            p.FaxNo = FaxNo.Trim();
            p.OfficeNo = OfficeNo.Trim();
            p.Email = Email.Trim();

            int pid;
            if (PatientId > 0)
            {
                await _db.UpdatePatient(p);
                pid = PatientId;
            }
            else
            {
                await _db.AddPatient(p);
                pid = p.PatientID;
            }

            if (!string.IsNullOrWhiteSpace(GuardianName))
                await _db.SaveGuardian(new Guardian
                {
                    PatientID = pid,
                    GuardianName = GuardianName.Trim(),
                    RelationshipToPatient = GuardianRelationship,
                    Occupation = GuardianOccupation.Trim(),
                    MobileNo = GuardianMobileNo.Trim(),
                });

            await _db.SaveMedicalHistory(new MedicalHistory
            {
                PatientID = pid,
                BloodType = BloodType,
                IsGoodHealth = IsGoodHealth,
                IsPregnant = IsPregnant,
                UnderMedicalTreatment = UnderMedicalTreatment,
                MedicationDetails = MedicationDetails.Trim(),
                HasBeenHospitalized = HasBeenHospitalized,
                HospitalizationDetails = HospitalizationDetails.Trim(),
                UsesTobacco = UsesTobacco,
                TakingMedications = TakingMedications,
            });

            await _db.SaveAllergy(new Allergy
            {
                PatientID = pid,
                HasLatexAllergy = HasLatexAllergy,
                HasAspirinAllergy = HasAspirinAllergy,
                HasPenicillinAllergy = HasPenicillinAllergy,
                HasSulfaAllergy = HasSulfaAllergy,
                HasLocalAnestheticAllergy = HasLocalAnestheticAllergy,
                OtherAllergy = OtherAllergy.Trim(),
            });

            var selectedIds = Conditions
                .Where(c => c.IsSelected)
                .Select(c => c.ConditionID)
                .ToList();
            await _db.SavePatientConditions(pid, selectedIds);

            // ── 2. Sync to Supabase — errors shown to user ────────────
            await SyncToSupabaseAsync(pid);

            await MainThread.InvokeOnMainThreadAsync(async () =>
                await Shell.Current.GoToAsync(".."));
        }
        catch (Exception ex)
        {
            await Shell.Current.DisplayAlert("Error", ex.Message, "OK");
        }
        finally { IsBusy = false; }
    }

    private async Task SyncToSupabaseAsync(int localPid)
    {
        // Read fresh from SQLite — never use ViewModel fields directly
        var saved = await _db.GetPatientById(localPid);
        if (saved == null)
        {
            System.Diagnostics.Debug.WriteLine("[Sync] Patient not found in SQLite");
            return;
        }

        // Use SupabaseId from local record if _supabaseId is empty
        if (string.IsNullOrEmpty(_supabaseId))
            _supabaseId = saved.SupabaseId ?? string.Empty;

        System.Diagnostics.Debug.WriteLine(
            $"[Sync] pid={localPid} supabaseId='{_supabaseId}' name={saved.FirstName} {saved.LastName}");

        // Read related tables
        var g = await _db.GetGuardianByPatient(localPid);
        var m = await _db.GetMedicalHistory(localPid);
        var a = await _db.GetAllergy(localPid);
        var conds = await _db.GetPatientConditions(localPid);
        var allConds = await _db.GetAllConditions();
        var condNames = string.Join(",", conds
            .Select(pc => allConds.FirstOrDefault(c => c.ConditionID == pc.ConditionID)?.ConditionName)
            .Where(n => n != null));

        // Build from SQLite data — guaranteed to have the saved values
        var sp = new SupabasePatient
        {
            FirstName = saved.FirstName,
            LastName = saved.LastName,
            Nickname = saved.Nickname,
            Gender = saved.Gender,
            DateOfBirth = DateTime.TryParse(saved.DateOfBirth, out var dob) ? dob : null,
            Nationality = saved.Nationality,
            Religion = saved.Religion,
            Occupation = saved.Occupation,
            Address = saved.Address,
            Phone = saved.MobileNo,
            HomeNo = saved.HomeNo,
            OfficeNo = saved.OfficeNo,
            FaxNo = saved.FaxNo,
            Email = saved.Email,
            DateRegistered = DateTime.TryParse(saved.DateRegistered, out var reg)
                     ? reg.ToUniversalTime()  // ← Supabase needs UTC
                     : DateTime.UtcNow,
            GuardianName = g?.GuardianName,
            GuardianRelationship = g?.RelationshipToPatient,
            GuardianOccupation = g?.Occupation,
            GuardianMobile = g?.MobileNo,
            BloodType = m?.BloodType,
            LatexAllergy = a?.HasLatexAllergy ?? false,
            AspirinAllergy = a?.HasAspirinAllergy ?? false,
            PenicillinAllergy = a?.HasPenicillinAllergy ?? false,
            SulfaAllergy = a?.HasSulfaAllergy ?? false,
            LocalAnestheticAllergy = a?.HasLocalAnestheticAllergy ?? false,
            OtherAllergy = a?.OtherAllergy,
            Conditions = condNames
        };

        if (!string.IsNullOrEmpty(_supabaseId))
        {
            // UPDATE — throws on failure so user sees the error
            sp.Id = _supabaseId;
            System.Diagnostics.Debug.WriteLine($"[Sync] Updating {_supabaseId}");
            var ok = await _supabase.UpdatePatientAsync(sp);
            System.Diagnostics.Debug.WriteLine($"[Sync] Update result: {ok}");

            if (!ok)
                await Shell.Current.DisplayAlert("Sync Warning",
                    "Patient saved locally but could not update cloud. Check your internet connection.",
                    "OK");
        }
        else
        {
            // INSERT — throws on failure so user sees the error
            System.Diagnostics.Debug.WriteLine("[Sync] Inserting new patient to Supabase");
            var inserted = await _supabase.AddPatientAsync(sp);

            if (inserted != null && !string.IsNullOrEmpty(inserted.Id))
            {
                _supabaseId = inserted.Id;
                saved.SupabaseId = inserted.Id;
                await _db.UpdatePatient(saved);
                System.Diagnostics.Debug.WriteLine(
                    $"[Sync] Success. SupabaseId={inserted.Id} saved locally");
            }
            else
            {
                System.Diagnostics.Debug.WriteLine("[Sync] Insert returned null");
                await Shell.Current.DisplayAlert("Sync Warning",
                    "Patient saved locally but could not sync to cloud.\n" +
                    "Check internet connection and Supabase RLS policies.",
                    "OK");
            }
        }
    }
}

public partial class ConditionCheckItem : ObservableObject
{
    public int ConditionID { get; set; }
    public string ConditionName { get; set; } = string.Empty;
    [ObservableProperty] bool isSelected;
    public bool IsNotOther => ConditionName != "Other";
}
