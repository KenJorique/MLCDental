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

    // ── Timestamps ────────────────────────────────────────────
    [ObservableProperty] string personalLastUpdated = string.Empty;
    [ObservableProperty] string medicalLastUpdated = string.Empty;

    // ── Personal Info ─────────────────────────────────────────
    [ObservableProperty] string fullName = string.Empty;
    [ObservableProperty] string firstName = string.Empty;
    [ObservableProperty] string lastName = string.Empty;
    [ObservableProperty] string nickname = string.Empty;

    public List<string> GenderOptions { get; } = new() { "Male", "Female", "Other" };
    public List<string> BloodTypeOptions { get; } = new() { "A+", "A-", "B+", "B-", "AB+", "AB-", "O+", "O-", "Unknown" };
    [ObservableProperty] string gender = string.Empty;

    [ObservableProperty] DateTime dateOfBirthDate = new DateTime(2000, 1, 1);
    [ObservableProperty] string dateOfBirthDisplay = string.Empty;
    [ObservableProperty] int age;

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

    // ── Contact ───────────────────────────────────────────────
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

    // ── Medical History (full) ────────────────────────────────
    [ObservableProperty] string bloodType = string.Empty;
    [ObservableProperty] bool isGoodHealth;
    [ObservableProperty] bool isPregnant;
    [ObservableProperty] bool underMedicalTreatment;
    [ObservableProperty] string medicationDetails = string.Empty;
    [ObservableProperty] bool hasBeenHospitalized;
    [ObservableProperty] string hospitalizationDetails = string.Empty;
    [ObservableProperty] bool usesTobacco;
    [ObservableProperty] bool takingMedications;

    // Computed — drives IsVisible of Pregnant field
    public bool IsFemale => Gender?.Equals("Female", StringComparison.OrdinalIgnoreCase) ?? false;

    // Notify IsFemale when Gender changes
    partial void OnGenderChanged(string value) => OnPropertyChanged(nameof(IsFemale));

    // ── Allergies ─────────────────────────────────────────────
    [ObservableProperty] bool hasLatexAllergy;
    [ObservableProperty] bool hasAspirinAllergy;
    [ObservableProperty] bool hasPenicillinAllergy;
    [ObservableProperty] bool hasSulfaAllergy;
    [ObservableProperty] bool hasLocalAnestheticAllergy;
    [ObservableProperty] string otherAllergy = string.Empty;

    // ── Medical Conditions ────────────────────────────────────
    public ObservableCollection<ConditionCheckItem> Conditions { get; } = new();
    [ObservableProperty] string conditionsText = "None reported";
    [ObservableProperty] string otherCondition = string.Empty;

    // Shows "Other" in view mode only when it has content
    public bool HasOtherCondition => !string.IsNullOrWhiteSpace(OtherCondition);
    partial void OnOtherConditionChanged(string value) =>
        OnPropertyChanged(nameof(HasOtherCondition));

    partial void OnPatientIdChanged(int value)
    {
        if (value > 0)
            MainThread.BeginInvokeOnMainThread(async () => await LoadPatientAsync(value));
    }

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
            DateRegistered = DateTime.TryParse(p.DateRegistered, out var reg)
                             ? reg.ToString("MMMM dd, yyyy") : p.DateRegistered;

            if (DateTime.TryParse(p.DateOfBirth, out var dob))
                DateOfBirthDate = dob;
            else
                DateOfBirthDate = new DateTime(2000, 1, 1);

            PersonalLastUpdated = !string.IsNullOrWhiteSpace(p.LastUpdated)
                ? $"Last updated: {p.LastUpdated}"
                : "Not yet updated";

            var g = await _db.GetGuardianByPatient(id);
            if (g is not null)
            {
                GuardianName = g.GuardianName;
                GuardianRelationship = g.RelationshipToPatient;
                GuardianOccupation = g.Occupation;
                GuardianMobile = g.MobileNo;
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
                OtherCondition = m.OtherCondition;
                MedicalLastUpdated = !string.IsNullOrWhiteSpace(m.LastUpdated)
                    ? (DateTime.TryParse(m.LastUpdated, out var parsedDate)
                        ? $"Last updated: {parsedDate:MMMM dd, yyyy h:mm tt}"
                        : $"Last updated: {m.LastUpdated}")
                    : "Not yet updated";
            }
            else
            {
                OtherCondition = string.Empty;
                MedicalLastUpdated = "Not yet updated";
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
        var parts = new List<string>();

        if (patientConds.Count > 0)
        {
            var allConds = await _db.GetAllConditions();
            var matched = allConds
                .Where(c => patientConds.Any(pc => pc.ConditionID == c.ConditionID))
                .Select(c => c.ConditionName).ToList();
            parts.AddRange(matched);
        }

        // Include free-text Other condition in the summary
        if (!string.IsNullOrWhiteSpace(OtherCondition))
            parts.Add(OtherCondition.Trim());

        ConditionsText = parts.Count > 0 ? string.Join(", ", parts) : "None reported";
    }

    private async Task LoadConditionsAsync()
    {
        await _db.EnsureDefaultConditions();
        var allConds = await _db.GetAllConditions();
        var patientConds = await _db.GetPatientConditions(PatientId);
        var selectedIds = patientConds.Select(pc => pc.ConditionID).ToHashSet();

        Conditions.Clear();
        // Alphabetical, "Other" excluded from checklist (shown as text entry instead)
        var sorted = allConds
            .Where(c => c.ConditionName != "Other")
            .OrderBy(c => c.ConditionName);
        foreach (var c in sorted)
            Conditions.Add(new ConditionCheckItem
            {
                ConditionID = c.ConditionID,
                ConditionName = c.ConditionName,
                IsSelected = selectedIds.Contains(c.ConditionID)
            });
    }

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

    [RelayCommand]
    async Task UpdateMedicalRecord()
    {
        IsSavingMedical = true;
        try
        {
            string today = DateTime.Now.ToString("MMMM dd, yyyy h:mm tt");

            await _db.SaveMedicalHistory(new MedicalHistory
            {
                PatientID = PatientId,
                BloodType = BloodType,
                IsGoodHealth = IsGoodHealth,
                IsPregnant = IsPregnant,
                UnderMedicalTreatment = UnderMedicalTreatment,
                MedicationDetails = MedicationDetails,
                HasBeenHospitalized = HasBeenHospitalized,
                HospitalizationDetails = HospitalizationDetails,
                UsesTobacco = UsesTobacco,
                TakingMedications = TakingMedications,
                OtherCondition = OtherCondition.Trim(),
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

            var selectedIds = Conditions.Where(c => c.IsSelected).Select(c => c.ConditionID).ToList();
            await _db.SavePatientConditions(PatientId, selectedIds);

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
}
