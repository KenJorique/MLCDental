using ClinicApp.Models;
using CommunityToolkit.Mvvm.ComponentModel;

namespace ClinicApp.ViewModels.PatientsRelatedVM
{
    // Wraps a Patient with UI state (IsExpanded) for the expandable card in PatientListPage.
    // We need this because the Patient model itself should not hold UI state.
    public partial class PatientCardViewModel : ObservableObject
    {
        // The underlying patient data
        public Patient Patient { get; }

        // Controls whether the 4 action buttons are visible on the card
        [ObservableProperty]
        bool isExpanded;

        public PatientCardViewModel(Patient patient)
        {
            Patient = patient;
        }
    }
}
