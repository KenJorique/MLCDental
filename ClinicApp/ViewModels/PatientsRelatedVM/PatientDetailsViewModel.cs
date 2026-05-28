
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

    // Personal
    [ObservableProperty] string fullName = string.Empty;
    [ObservableProperty] string nickname = string.Empty;
    [ObservableProperty] string gender = string.Empty;
    [ObservableProperty] string dateOfBirth = string.Empty;
    [ObservableProperty] int age;
    [ObservableProperty] string nationality = string.Empty;
    [ObservableProperty] string religion = string.Empty;
    [ObservableProperty] string occupation = string.Empty;
    [ObservableProperty] string address = string.Empty;

    // Contact
    [ObservableProperty] string mobileNo = string.Empty;
    [ObservableProperty] string homeNo = string.Empty;
    [ObservableProperty] string faxNo = string.Empty;
    [ObservableProperty] string officeNo = string.Empty;
    [ObservableProperty] string email = string.Empty;

    // Referral / Insurance
    [ObservableProperty] string referredBy = string.Empty;
    [ObservableProperty] string reasonForConsultation = string.Empty;
    [ObservableProperty] string dentalInsurance = string.Empty;
    [ObservableProperty] string insuranceEffectiveDate = string.Empty;
    [ObservableProperty] bool hasInsurance;

    // Guardian
    [ObservableProperty] string guardianName = string.Empty;
    [ObservableProperty] string guardianRelationship = string.Empty;
    [ObservableProperty] string guardianOccupation = string.Empty;
    [ObservableProperty] string guardianMobile = string.Empty;
    [ObservableProperty] bool hasGuardian;

    // Medical History
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

    // Allergies
    [ObservableProperty] string allergyText = "None reported";

    // Conditions
    [ObservableProperty] string conditionsText = "None reported";

    [ObservableProperty] string dateRegistered = string.Empty;

    partial void OnPatientIdChanged(int value)
    {
        if (value > 0)
            MainThread.BeginInvokeOnMainThread(async () => await LoadPatientAsync(value));
    }

    [RelayCommand]
    public async Task LoadPatient()
    {
        // Reset IsBusy in case a previous load left it stuck true
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
            Nickname = p.Nickname;
            Gender = p.Gender;
            DateOfBirth = DateTime.TryParse(p.DateOfBirth, out var dob)
                          ? dob.ToString("MMMM dd, yyyy") : p.DateOfBirth;
            Age = p.Age;
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
            InsuranceEffectiveDate = DateTime.TryParse(p.InsuranceEffectiveDate, out var ins)
                                     ? ins.ToString("MMMM dd, yyyy") : string.Empty;
            DateRegistered = DateTime.TryParse(p.DateRegistered, out var reg)
                             ? reg.ToString("MMMM dd, yyyy") : p.DateRegistered;

            // Guardian
            var g = await _db.GetGuardianByPatient(id);
            HasGuardian = g is not null && !string.IsNullOrWhiteSpace(g.GuardianName);
            if (HasGuardian && g is not null)
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
            }

            // Allergies — build a readable summary
            var a = await _db.GetAllergy(id);
            if (a is not null)
            {
                var list = new List<string>();
                if (a.HasLatexAllergy) list.Add("Latex");
                if (a.HasAspirinAllergy) list.Add("Aspirin");
                if (a.HasPenicillinAllergy) list.Add("Penicillin");
                if (a.HasSulfaAllergy) list.Add("Sulfa");
                if (a.HasLocalAnestheticAllergy) list.Add("Local Anesthetic");
                if (!string.IsNullOrWhiteSpace(a.OtherAllergy)) list.Add(a.OtherAllergy);
                AllergyText = list.Count > 0 ? string.Join(", ", list) : "None reported";
            }

            // Conditions
            var patientConds = await _db.GetPatientConditions(id);
            if (patientConds.Count > 0)
            {
                var allConds = await _db.GetAllConditions();
                var matched = allConds.Where(c => patientConds.Any(pc => pc.ConditionID == c.ConditionID))
                                       .Select(c => c.ConditionName).ToList();
                ConditionsText = matched.Count > 0 ? string.Join(", ", matched) : "None reported";
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[PatientDetails] {ex.Message}");
        }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    async Task ShowMenu()
    {
        string action = await Shell.Current.DisplayActionSheet(
            "Options", "Cancel", null, "Edit", "Delete");

        if (action == "Edit")
            await Shell.Current.GoToAsync($"{nameof(AddPatientPage)}?PatientId={PatientId}");
        else if (action == "Delete")
        {
            bool ok = await Shell.Current.DisplayAlert("Confirm Delete",
                $"Delete {FullName} and all their records?", "Yes, Delete", "Cancel");
            if (!ok) return;
            var p = await _db.GetPatientById(PatientId);
            if (p is not null) await _db.DeletePatient(p);
            await Shell.Current.GoToAsync("..");
        }
    }
}
