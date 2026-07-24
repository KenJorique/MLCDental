using ClinicApp.Models;
using CommunityToolkit.Mvvm.ComponentModel;

namespace ClinicApp.ViewModels.PatientsRelatedVM
{
    public partial class PatientCardViewModel : ObservableObject
    {
        public Patient Patient { get; }

        [ObservableProperty]
        bool isExpanded;

        public PatientCardViewModel(Patient patient)
        {
            Patient = patient;
        }

        /// Displays as "LastName, FirstName" e.g. "Bolasco, Leah Marie"
        public string DisplayName
        {
            get
            {
                var last = Patient.LastName?.Trim() ?? string.Empty;
                var first = Patient.FirstName?.Trim() ?? string.Empty;

                if (string.IsNullOrEmpty(last)) return first;
                if (string.IsNullOrEmpty(first)) return last;

                return $"{last}, {first}";
            }
        }
    }
}
