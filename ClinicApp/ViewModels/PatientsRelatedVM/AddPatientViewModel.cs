using ClinicApp.Models;
using ClinicApp.Helpers;
using ClinicApp.Models.PatientModels;
using ClinicApp.Models.SupabaseModels;
using ClinicApp.Services;
using ClinicApp.Views.Shared;
using CommunityToolkit.Maui.Views;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using System.ComponentModel;

namespace ClinicApp.ViewModels.PatientsRelatedVM;

[QueryProperty(nameof(PatientId), "PatientId")]
public partial class AddPatientViewModel : ObservableObject
{
    readonly DatabaseService _db;
    readonly SupabaseDataService _supabase;

    // Tracks the Supabase UUID when editing an existing patient.
    private string _supabaseId = string.Empty;

    // Tracks whether the user has made any unsaved edits, so Cancel / the
    // back arrow know whether a "discard changes?" prompt is actually needed.
    private bool _isLoading;
    private bool _isDirty;

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
    [ObservableProperty] string treatmentDetails = string.Empty;
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

    // Refreshes IsMinor whenever the birthdate changes.
    partial void OnDateOfBirthChanged(DateTime value) =>
        OnPropertyChanged(nameof(IsMinor));

    // Shows Pregnant checkbox only for Female patients.
    public bool IsFemale => SelectedGender?.Equals("Female", StringComparison.OrdinalIgnoreCase) ?? false;

    // Refreshes IsFemale whenever gender changes.
    partial void OnSelectedGenderChanged(string value) =>
        OnPropertyChanged(nameof(IsFemale));

    // Marks the form dirty on any field edit, except computed/internal properties or during a load.
    // NOTE: this does NOT catch checkbox toggles inside the Conditions list — those live on
    // ConditionCheckItem, not on this ViewModel, so they don't flow through here. Flag if you want
    // condition changes to also count toward "unsaved changes."
    protected override void OnPropertyChanged(PropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);
        if (_isLoading) return;
        if (e.PropertyName is nameof(IsBusy) or nameof(PageTitle) or nameof(PatientId)
            or nameof(IsMinor) or nameof(IsFemale))
            return;
        _isDirty = true;
    }

    // Loads the condition checklist for a brand-new patient.
    public async Task InitializeAsync()
    {
        if (PatientId <= 0)
            await LoadConditionListAsync();
    }

    // Loads the full patient record for editing once PatientId is set via navigation.
    partial void OnPatientIdChanged(int value)
    {
        if (value > 0)
            MainThread.BeginInvokeOnMainThread(async () => await LoadForEditAsync(value));
    }

    // Loads the full condition checklist, unchecked, for a new patient.
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

    // Loads an existing patient's full record into the form for editing.
    private async Task LoadForEditAsync(int id)
    {
        IsBusy = true;
        _isLoading = true;
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
                TreatmentDetails = m.TreatmentDetails;
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
        finally
        {
            IsBusy = false;
            _isLoading = false;
            _isDirty = false; // freshly loaded data isn't a user edit
        }
    }

    // Validates the form, confirms with the popup, saves locally and to Supabase, and logs the activity.
    [RelayCommand]
    async Task SavePatient()
    {
        var errors = PatientValidator.Validate(
            FirstName, LastName, SelectedGender, Address, MobileNo, Email,
            IsMinor, GuardianName, GuardianMobileNo);
        if (errors.Count > 0)
        {
            var notice = new ConfirmationPopup(
                "Required Fields Missing",
                string.Join("\n", errors),
                confirmText: "OK",
                confirmColor: Colors.Green,
                showCancelButton: false);
            await Shell.Current.ShowPopupAsync(notice);
            return;
        }

        bool isNewPatient = PatientId <= 0;

        var confirmPopup = new ConfirmationPopup(
            "Save Patient?",
            isNewPatient
                ? $"Add {FirstName.Trim()} {LastName.Trim()} as a new patient?"
                : $"Save changes to {FirstName.Trim()} {LastName.Trim()}?",
            confirmText: "Save",
            confirmColor: Colors.Green);

        var confirmResult = await Shell.Current.ShowPopupAsync(confirmPopup);
        if (confirmResult is not bool confirmed || !confirmed) return;

        IsBusy = true;
        try
        {
            // Clear a detail field once its checkbox is off, so an old value can't resurface if it's checked again later.
            if (!UnderMedicalTreatment) TreatmentDetails = string.Empty;
            if (!HasBeenHospitalized) HospitalizationDetails = string.Empty;
            if (!TakingMedications) MedicationDetails = string.Empty;

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
                TreatmentDetails = TreatmentDetails.Trim(),
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

            await _supabase.LogActivityAsync(
                isNewPatient ? "NewPatient" : "PatientUpdated",
                isNewPatient ? $"New patient {p.FullName} added" : $"{p.FullName}'s info was updated");

            _isDirty = false;

            await MainThread.InvokeOnMainThreadAsync(async () =>
                await Shell.Current.GoToAsync(".."));
        }
        catch (Exception ex)
        {
            await Shell.Current.DisplayAlert("Error", ex.Message, "OK");
        }
        finally { IsBusy = false; }
    }

    // Confirms discard with the popup if there are unsaved edits, then goes back.
    [RelayCommand]
    async Task CancelAsync()
    {
        if (_isDirty)
        {
            var popup = new ConfirmationPopup(
                "Discard Changes?",
                "Are you sure you want to discard the changes you made?",
                confirmText: "Discard",
                confirmColor: Colors.Crimson);

            var result = await Shell.Current.ShowPopupAsync(popup);
            if (result is not bool discard || !discard)
                return;
        }

        await Shell.Current.GoToAsync("..");
    }

    // Saves locally then pushes the full record to Supabase, insert or update as appropriate.
    // Guardian and Conditions are both normalized tables in Supabase (SupabasePatient only holds
    // a GuardianId foreign key and has no Conditions column at all), so both are resolved/synced
    // through their own dedicated methods rather than written as flat fields on the patient row.
    // Saves locally already happened by the time this runs. This just pushes to Supabase
    // and, on failure, shows a soft warning instead of blocking navigation — the patient
    // is safe locally either way, and SyncPendingPatientsAsync will retry automatically.
    private async Task SyncToSupabaseAsync(int localPid)
    {
        string? result;
        try
        {
            result = await _db.SyncPatientToSupabaseAsync(localPid, _supabaseId);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[Sync] Unexpected error: {ex.Message}");
            result = null;
        }

        if (!string.IsNullOrEmpty(result))
        {
            _supabaseId = result;
            return;
        }

        await Shell.Current.DisplayAlert("Sync Warning",
            "Patient saved locally but could not sync to the cloud yet. " +
            "It will sync automatically once you're back online.",
            "OK");
    }
}

public partial class ConditionCheckItem : ObservableObject
{
    public int ConditionID { get; set; }
    public string ConditionName { get; set; } = string.Empty;
    [ObservableProperty] bool isSelected;
    public bool IsNotOther => ConditionName != "Other";
}
