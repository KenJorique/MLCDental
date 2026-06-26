using ClinicApp.Models;
using ClinicApp.Services;
using ClinicApp.Views.PatientsRelated;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;

namespace ClinicApp.ViewModels.PatientsRelatedVM;

[QueryProperty(nameof(PatientId), "id")]
public partial class PatientDetailsViewModel : ObservableObject
{
    readonly DatabaseService _db;
    public PatientDetailsViewModel(DatabaseService db) => _db = db;

    [ObservableProperty] int patientId;
    [ObservableProperty] bool isBusy;
    [ObservableProperty] bool isSavingPersonal;
    [ObservableProperty] bool isSavingMedical;

    // ── Tab state ─────────────────────────────────────────────
    [ObservableProperty] bool isPersonalTabActive = true;
    [ObservableProperty] bool isMedicalTabActive = false;

    [RelayCommand]
    void SelectPersonalTab()
    {
        IsPersonalTabActive = true;
        IsMedicalTabActive = false;
        IsPersonalEditMode = false;
        IsMedicalEditMode = false;
    }

    [RelayCommand]
    void SelectMedicalTab()
    {
        IsPersonalTabActive = false;
        IsMedicalTabActive = true;
        IsPersonalEditMode = false;
        IsMedicalEditMode = false;
    }

    // ── Edit mode ─────────────────────────────────────────────
    [ObservableProperty] bool isPersonalEditMode = false;
    [ObservableProperty] bool isMedicalEditMode = false;

    [RelayCommand]
    void TogglePersonalEdit() => IsPersonalEditMode = !IsPersonalEditMode;

    [RelayCommand]
    void ToggleMedicalEdit()
    {
        IsMedicalEditMode = !IsMedicalEditMode;
        if (IsMedicalEditMode && Conditions.Count == 0)
            MainThread.BeginInvokeOnMainThread(async () => await LoadConditionsAsync());
    }

    // ── Last updated timestamps ───────────────────────────────
    // Both are always loaded from DB — never left empty after first save
    [ObservableProperty] string personalLastUpdated = string.Empty;
    [ObservableProperty] string medicalLastUpdated = string.Empty;

    // ── Personal Info fields ──────────────────────────────────
    [ObservableProperty] string fullName = string.Empty;
    [ObservableProperty] string firstName = string.Empty;
    [ObservableProperty] string lastName = string.Empty;
    [ObservableProperty] string nickname = string.Empty;

    public List<string> GenderOptions { get; } = new() { "Male", "Female", "Other" };
    [ObservableProperty] string gender = string.Empty;

    // DateOfBirth as DateTime for DatePicker
    [ObservableProperty] DateTime dateOfBirthDate = new DateTime(2000, 1, 1);
    [ObservableProperty] string dateOfBirthDisplay = string.Empty;
    [ObservableProperty] int age;

    // Auto-compute age and display string when date changes
    partial void OnDateOfBirthDateChanged(DateTime value)
    {
        var today = DateTime.Today;
        var a = today.Year - value.Year;
        if (value.Date > today.AddYears(-a)) a--;
        Age = a;
        DateOfBirthDisplay = value.ToString("MMMM dd, yyyy");
    }

    [ObservableProperty] string nationality = string.Empty;
    [ObservableProperty] string religion = string.Empty;
    [ObservableProperty] string occupation = string.Empty;
    [ObservableProperty] string address = string.Empty;
    [ObservableProperty] string dateRegistered = string.Empty;

    // ── Contact fields ────────────────────────────────────────
    [ObservableProperty] string mobileNo = string.Empty;
    [ObservableProperty] string homeNo = string.Empty;
    [ObservableProperty] string faxNo = string.Empty;
    [ObservableProperty] string officeNo = string.Empty;
    [ObservableProperty] string email = string.Empty;

    // ── Guardian ──────────────────────────────────────────────
    [ObservableProperty] string guardianName = string.Empty;
    [ObservableProperty] string guardianRelationship = string.Empty;
    [ObservableProperty] string guardianOccupation = string.Empty;
    [ObservableProperty] string guardianMobile = string.Empty;

    // ── Referral / Insurance ──────────────────────────────────
    [ObservableProperty] string referredBy = string.Empty;
    [ObservableProperty] string reasonForConsultation = string.Empty;
    [ObservableProperty] string dentalInsurance = string.Empty;
    [ObservableProperty] string insuranceEffectiveDate = string.Empty;

    // ── Medical History ───────────────────────────────────────
    [ObservableProperty] string bloodType = string.Empty;
    [ObservableProperty] string physicianName = string.Empty;
    [ObservableProperty] bool isGoodHealth;
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

    // ── Allergies ─────────────────────────────────────────────
    [ObservableProperty] bool hasLatexAllergy;
    [ObservableProperty] bool hasAspirinAllergy;
    [ObservableProperty] bool hasPenicillinAllergy;
    [ObservableProperty] bool hasSulfaAllergy;
    [ObservableProperty] bool hasLocalAnestheticAllergy;
    [ObservableProperty] string otherAllergy = string.Empty;

    // ── Medical Conditions checklist ──────────────────────────
    public ObservableCollection<ConditionCheckItem> Conditions { get; } = new();
    [ObservableProperty] string conditionsText = "None reported";

    // Auto-load when PatientId arrives from navigation
    partial void OnPatientIdChanged(int value)
    {
        if (value > 0)
            MainThread.BeginInvokeOnMainThread(async () => await LoadPatientAsync(value));
    }

    // Public reload — called by OnNavigatedTo (also after editing)
    [RelayCommand]
    public async Task LoadPatient()
    {
        IsBusy = false;
        if (PatientId > 0)
            await LoadPatientAsync(PatientId);
    }

    private async Task LoadPatientAsync(int id)
    {
        IsBusy = true;
        try
        {
            var p = await _db.GetPatientById(id);
            if (p is null) return;

            FullName = p.FullName;
            FirstName = p.FirstName;
            LastName = p.LastName;
            Nickname = p.Nickname;
            Gender = p.Gender;
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
            InsuranceEffectiveDate = DateTime.TryParse(p.InsuranceEffectiveDate, out var ins)
                                     ? ins.ToString("MMMM dd, yyyy") : string.Empty;
            DateRegistered = DateTime.TryParse(p.DateRegistered, out var reg)
                             ? reg.ToString("MMMM dd, yyyy") : p.DateRegistered;

            // DateOfBirth → DatePicker
            if (DateTime.TryParse(p.DateOfBirth, out var dob))
                DateOfBirthDate = dob;
            else
                DateOfBirthDate = new DateTime(2000, 1, 1);

            // ── PersonalLastUpdated: always read from DB ──────
            // Show timestamp if saved, otherwise show "Never updated"
            PersonalLastUpdated = !string.IsNullOrWhiteSpace(p.LastUpdated)
                ? $"Last updated: {p.LastUpdated}"
                : "Not yet updated";

            // Guardian
            var g = await _db.GetGuardianByPatient(id);
            if (g is not null)
            {
                GuardianName = g.GuardianName;
                GuardianRelationship = g.RelationshipToPatient;
                GuardianOccupation = g.Occupation;
                GuardianMobile = g.MobileNo;
            }

            // Medical History
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

                // ── MedicalLastUpdated: always read from DB ───
                // Parse and reformat to match Personal Info style
                if (!string.IsNullOrWhiteSpace(m.LastUpdated))
                {
                    // If already in long format keep it, else try to parse and reformat
                    if (DateTime.TryParse(m.LastUpdated, out var parsedDate))
                        MedicalLastUpdated = $"Last updated: {parsedDate:MMMM dd, yyyy h:mm tt}";
                    else
                        MedicalLastUpdated = $"Last updated: {m.LastUpdated}";
                }
                else
                {
                    MedicalLastUpdated = "Not yet updated";
                }
            }
            else
            {
                MedicalLastUpdated = "Not yet updated";
            }

            // Allergies
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

            // Conditions summary
            await BuildConditionsSummaryAsync(id);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[PatientDetails] {ex.Message}");
        }
        finally { IsBusy = false; }
    }

    private async Task BuildConditionsSummaryAsync(int patientId)
    {
        var patientConds = await _db.GetPatientConditions(patientId);
        if (patientConds.Count > 0)
        {
            var allConds = await _db.GetAllConditions();
            var matched = allConds
                .Where(c => patientConds.Any(pc => pc.ConditionID == c.ConditionID))
                .Select(c => c.ConditionName).ToList();
            ConditionsText = matched.Count > 0 ? string.Join(", ", matched) : "None reported";
        }
        else ConditionsText = "None reported";
    }

    private async Task LoadConditionsAsync()
    {
        await _db.EnsureDefaultConditions();
        var allConds = await _db.GetAllConditions();
        var patientConds = await _db.GetPatientConditions(PatientId);
        var selectedIds = patientConds.Select(pc => pc.ConditionID).ToHashSet();

        Conditions.Clear();
        foreach (var c in allConds)
            Conditions.Add(new ConditionCheckItem
            {
                ConditionID = c.ConditionID,
                ConditionName = c.ConditionName,
                IsSelected = selectedIds.Contains(c.ConditionID)
            });
    }

    // Saves Personal Info + Guardian, stamps LastUpdated
    [RelayCommand]
    async Task SavePersonalRecord()
    {
        IsSavingPersonal = true;
        try
        {
            var p = await _db.GetPatientById(PatientId);
            if (p is null) return;

            p.FirstName = FirstName.Trim();
            p.LastName = LastName.Trim();
            p.Nickname = Nickname.Trim();
            p.Gender = Gender;
            p.DateOfBirth = DateOfBirthDate.ToString("yyyy-MM-dd");
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
            p.DentalInsurance = DentalInsurance.Trim();

            // Stamp the timestamp — this is what persists in DB
            string today = DateTime.Now.ToString("MMMM dd, yyyy h:mm tt");
            p.LastUpdated = today;

            await _db.UpdatePatient(p);

            await _db.SaveGuardian(new Guardian
            {
                PatientID = PatientId,
                GuardianName = GuardianName.Trim(),
                RelationshipToPatient = GuardianRelationship,
                Occupation = GuardianOccupation.Trim(),
                MobileNo = GuardianMobile.Trim(),
            });

            // Update UI immediately — will also reload from DB on next navigate
            FullName = p.FullName;
            PersonalLastUpdated = $"Last updated: {today}";
            IsPersonalEditMode = false;
        }
        catch (Exception ex)
        {
            await Shell.Current.DisplayAlert("Error", ex.Message, "OK");
        }
        finally { IsSavingPersonal = false; }
    }

    // Saves Medical History, Allergies, Conditions, stamps LastUpdated
    [RelayCommand]
    async Task UpdateMedicalRecord()
    {
        IsSavingMedical = true;
        try
        {
            // Same date+time format as Personal Info for consistency
            string today = DateTime.Now.ToString("MMMM dd, yyyy h:mm tt");

            await _db.SaveMedicalHistory(new MedicalHistory
            {
                PatientID = PatientId,
                BloodType = BloodType,
                PhysicianName = PhysicianName,
                IsGoodHealth = IsGoodHealth,
                IsPregnant = IsPregnant,
                UnderMedicalTreatment = UnderMedicalTreatment,
                MedicationDetails = MedicationDetails,
                HasBeenHospitalized = HasBeenHospitalized,
                HospitalizationDetails = HospitalizationDetails,
                BloodPressure = BloodPressure,
                BleedingTime = BleedingTime,
                UsesTobacco = UsesTobacco,
                UsesAlcohol = UsesAlcohol,
                TakingMedications = TakingMedications,
                PreviousDentist = PreviousDentist,
                LastDentalVisit = LastDentalVisit,
                LastUpdated = today,
            });

            await _db.SaveAllergy(new Allergy
            {
                PatientID = PatientId,
                HasLatexAllergy = HasLatexAllergy,
                HasAspirinAllergy = HasAspirinAllergy,
                HasPenicillinAllergy = HasPenicillinAllergy,
                HasSulfaAllergy = HasSulfaAllergy,
                HasLocalAnestheticAllergy = HasLocalAnestheticAllergy,
                OtherAllergy = OtherAllergy,
            });

            var selectedIds = Conditions
                .Where(c => c.IsSelected)
                .Select(c => c.ConditionID).ToList();
            await _db.SavePatientConditions(PatientId, selectedIds);

            // Update UI immediately with same format
            MedicalLastUpdated = $"Last updated: {today}";

            await BuildConditionsSummaryAsync(PatientId);
            IsMedicalEditMode = false;
        }
        catch (Exception ex)
        {
            await Shell.Current.DisplayAlert("Error", ex.Message, "OK");
        }
        finally { IsSavingMedical = false; }
    }

    // Meatball menu — Delete patient
    [RelayCommand]
    async Task ShowMenu()
    {
        bool ok = await Shell.Current.DisplayAlert("Confirm Delete",
            $"Delete {FullName} and all their records?", "Yes, Delete", "Cancel");
        if (!ok) return;
        var p = await _db.GetPatientById(PatientId);
        if (p is not null) await _db.DeletePatient(p);
        await Shell.Current.GoToAsync("..");
    }
}