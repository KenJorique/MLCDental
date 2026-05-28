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
    public AddPatientViewModel(DatabaseService db) => _db = db;

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

    // ── Section B: Contact ────────────────────────────────────────
    [ObservableProperty] string mobileNo = string.Empty;
    [ObservableProperty] string homeNo = string.Empty;
    [ObservableProperty] string faxNo = string.Empty;
    [ObservableProperty] string officeNo = string.Empty;
    [ObservableProperty] string email = string.Empty;

    // ── Section C: Referral / Insurance ──────────────────────────
    [ObservableProperty] string referredBy = string.Empty;
    [ObservableProperty] string reasonForConsultation = string.Empty;
    [ObservableProperty] string dentalInsurance = string.Empty;
    [ObservableProperty] DateTime insuranceEffectiveDate = DateTime.Today;
    [ObservableProperty] bool hasInsurance;

    // ── Section D: Guardian ───────────────────────────────────────
    [ObservableProperty] string guardianName = string.Empty;
    [ObservableProperty] string guardianRelationship = string.Empty;
    [ObservableProperty] string guardianOccupation = string.Empty;
    [ObservableProperty] string guardianMobileNo = string.Empty;

    // ── Section E: Medical History ────────────────────────────────
    [ObservableProperty] string bloodType = string.Empty;
    [ObservableProperty] string physicianName = string.Empty;
    [ObservableProperty] bool isGoodHealth = true;
    [ObservableProperty] bool isPregnant;
    [ObservableProperty] bool underMedicalTreatment;
    [ObservableProperty] string medicationDetails = string.Empty;
    [ObservableProperty] bool hasBeenHospitalized;
    [ObservableProperty] string hospitalizationDetails = string.Empty;
    [ObservableProperty] string bloodPressure = string.Empty;
    [ObservableProperty] string bleedingTime = string.Empty;
    [ObservableProperty] bool usesTobacco;
    [ObservableProperty] bool usesAlcohol;
    [ObservableProperty] bool takingMedications;
    [ObservableProperty] string previousDentist = string.Empty;
    [ObservableProperty] string lastDentalVisit = string.Empty;

    // ── Section F: Allergies ──────────────────────────────────────
    [ObservableProperty] bool hasLatexAllergy;
    [ObservableProperty] bool hasAspirinAllergy;
    [ObservableProperty] bool hasPenicillinAllergy;
    [ObservableProperty] bool hasSulfaAllergy;
    [ObservableProperty] bool hasLocalAnestheticAllergy;
    [ObservableProperty] string otherAllergy = string.Empty;

    // ── Section G: Medical Conditions ─────────────────────────────
    public ObservableCollection<ConditionCheckItem> Conditions { get; } = new();

    public List<string> GenderOptions { get; } = new() { "Male", "Female", "Other" };
    public List<string> BloodTypeOptions { get; } = new() { "A+", "A-", "B+", "B-", "AB+", "AB-", "O+", "O-", "Unknown" };
    public List<string> RelationshipOptions { get; } = new() { "Parent", "Spouse", "Sibling", "Child", "Grandparent", "Guardian", "Other" };

    // ── Computed: is the patient a minor (under 18)? ──────────────
    // Recalculated whenever DateOfBirth changes
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

    // Notify IsMinor whenever DateOfBirth changes
    partial void OnDateOfBirthChanged(DateTime value) =>
        OnPropertyChanged(nameof(IsMinor));

    // Called by AddPatientPage.OnAppearing() — conditions always load for new patients.
    // OnPatientIdChanged never fires for value=0 because 0 is the field default.
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
        foreach (var c in all)
            Conditions.Add(new ConditionCheckItem { ConditionID = c.ConditionID, ConditionName = c.ConditionName });
    }

    private async Task LoadForEditAsync(int id)
    {
        IsBusy = true;
        try
        {
            PageTitle = "Edit Patient";
            var p = await _db.GetPatientById(id);
            if (p is null) return;

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
            ReferredBy = p.ReferredBy;
            ReasonForConsultation = p.ReasonForConsultation;
            DentalInsurance = p.DentalInsurance;
            HasInsurance = !string.IsNullOrEmpty(p.DentalInsurance);
            if (DateTime.TryParse(p.InsuranceEffectiveDate, out var ins)) InsuranceEffectiveDate = ins;

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
                PhysicianName = m.PhysicianName;
                IsGoodHealth = m.IsGoodHealth;
                IsPregnant = m.IsPregnant;
                UnderMedicalTreatment = m.UnderMedicalTreatment;
                MedicationDetails = m.MedicationDetails;
                HasBeenHospitalized = m.HasBeenHospitalized;
                HospitalizationDetails = m.HospitalizationDetails;
                BloodPressure = m.BloodPressure;
                BleedingTime = m.BleedingTime;
                UsesTobacco = m.UsesTobacco;
                UsesAlcohol = m.UsesAlcohol;
                TakingMedications = m.TakingMedications;
                PreviousDentist = m.PreviousDentist;
                LastDentalVisit = m.LastDentalVisit;
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
        // ── Validate required fields ──────────────────────────────
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(FirstName) || string.IsNullOrWhiteSpace(LastName))
            errors.Add("• First and last name are required.");
        if (string.IsNullOrWhiteSpace(SelectedGender))
            errors.Add("• Gender is required.");
        if (string.IsNullOrWhiteSpace(Address))
            errors.Add("• Address is required.");
        if (string.IsNullOrWhiteSpace(MobileNo))
            errors.Add("• Mobile number is required.");
        if (string.IsNullOrWhiteSpace(Email))
            errors.Add("• Email address is required.");
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
            p.ReferredBy = ReferredBy.Trim();
            p.ReasonForConsultation = ReasonForConsultation.Trim();
            p.DentalInsurance = HasInsurance ? DentalInsurance.Trim() : string.Empty;
            p.InsuranceEffectiveDate = HasInsurance ? InsuranceEffectiveDate.ToString("yyyy-MM-dd") : string.Empty;

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
                PhysicianName = PhysicianName.Trim(),
                IsGoodHealth = IsGoodHealth,
                IsPregnant = IsPregnant,
                UnderMedicalTreatment = UnderMedicalTreatment,
                MedicationDetails = MedicationDetails.Trim(),
                HasBeenHospitalized = HasBeenHospitalized,
                HospitalizationDetails = HospitalizationDetails.Trim(),
                BloodPressure = BloodPressure.Trim(),
                BleedingTime = BleedingTime.Trim(),
                UsesTobacco = UsesTobacco,
                UsesAlcohol = UsesAlcohol,
                TakingMedications = TakingMedications,
                PreviousDentist = PreviousDentist.Trim(),
                LastDentalVisit = LastDentalVisit.Trim(),
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

            var selectedIds = Conditions.Where(c => c.IsSelected).Select(c => c.ConditionID).ToList();
            await _db.SavePatientConditions(pid, selectedIds);

            await MainThread.InvokeOnMainThreadAsync(async () =>
                await Shell.Current.GoToAsync(".."));
        }
        catch (Exception ex)
        {
            await Shell.Current.DisplayAlert("Error", ex.Message, "OK");
        }
        finally { IsBusy = false; }
    }
}

/// <summary>UI helper for the conditions checklist.</summary>
public partial class ConditionCheckItem : ObservableObject
{
    public int ConditionID { get; set; }
    public string ConditionName { get; set; } = string.Empty;
    [ObservableProperty] bool isSelected;
}
